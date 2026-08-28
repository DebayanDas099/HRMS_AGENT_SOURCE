using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Helpers;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.En;
using Lucene.Net.Analysis.Miscellaneous;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Similarities;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.RAG.Services;

/// <summary>
/// Corpus-wide BM25 index over ingested chunks, backed by a local Lucene directory.
/// <para>
/// Registered as a singleton: opening the directory is expensive and only one
/// IndexWriter may hold the write lock, so this type owns the writer for the
/// lifetime of the process. That is safe for the single-instance deployment this
/// was built for; scaling out needs either a per-replica index rebuilt from Qdrant
/// or a shared mount with one designated writer.
/// </para>
/// </summary>
public sealed class LuceneLexicalIndex : ILexicalIndex, IDisposable
{
    private readonly RetrievalSettings _settings;
    private readonly ILogger<LuceneLexicalIndex> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Analyzer _analyzer;
    private readonly FSDirectory _directory;
    private readonly IndexWriter _writer;
    private readonly SearcherManager _searcherManager;
    private bool _disposed;

    public LuceneLexicalIndex(IOptions<RetrievalSettings> settings, ILogger<LuceneLexicalIndex> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var indexPath = ResolveIndexPath(_settings.LuceneIndexPath);
        System.IO.Directory.CreateDirectory(indexPath);

        _analyzer = BuildAnalyzer();
        _directory = FSDirectory.Open(new DirectoryInfo(indexPath));

        // An unclean shutdown leaves write.lock behind and the writer would never open again.
        if (IndexWriter.IsLocked(_directory))
        {
            _logger.LogWarning("Clearing stale Lucene write lock at {IndexPath}.", indexPath);
            IndexWriter.Unlock(_directory);
        }

        var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, _analyzer)
        {
            OpenMode = OpenMode.CREATE_OR_APPEND,
            Similarity = BuildSimilarity()
        };

        _writer = new IndexWriter(_directory, config);
        _searcherManager = new SearcherManager(_writer, applyAllDeletes: true, null);

        _logger.LogInformation(
            "Lucene lexical index opened at {IndexPath} with {DocumentCount} documents.",
            indexPath,
            _writer.NumDocs);
    }

    public Task<IReadOnlyList<RetrievalCandidate>> SearchAsync(
        string query,
        int limit,
        string? category,
        bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
        {
            return Task.FromResult<IReadOnlyList<RetrievalCandidate>>([]);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var searcher = _searcherManager.Acquire();

        try
        {
            // BM25 must be set on the searcher too; the writer config alone does not
            // affect query-time scoring, and Lucene 4.8 defaults to TF-IDF.
            searcher.Similarity = BuildSimilarity();

            var parsedQuery = LuceneQueryBuilder.BuildQuery(
                query,
                _analyzer,
                _settings.TitleBoost,
                _settings.SectionPathBoost);

            var filter = LuceneQueryBuilder.BuildFilter(category, activeOnly);
            var topDocs = searcher.Search(parsedQuery, filter, limit);

            var results = new List<RetrievalCandidate>(topDocs.ScoreDocs.Length);

            for (var index = 0; index < topDocs.ScoreDocs.Length; index++)
            {
                var scoreDoc = topDocs.ScoreDocs[index];
                var document = searcher.Doc(scoreDoc.Doc);
                var candidate = ToCandidate(document);

                candidate.SparseRank = index + 1;
                candidate.SparseScore = scoreDoc.Score;
                results.Add(candidate);
            }

            return Task.FromResult<IReadOnlyList<RetrievalCandidate>>(results);
        }
        catch (Lucene.Net.QueryParsers.Classic.ParseException ex)
        {
            // Escaped input should make this unreachable; degrade to dense-only rather than fail the query.
            _logger.LogWarning(ex, "Lucene could not parse query {Query}; skipping lexical retrieval.", query);
            return Task.FromResult<IReadOnlyList<RetrievalCandidate>>([]);
        }
        finally
        {
            _searcherManager.Release(searcher);
        }
    }

    public async Task ReplaceDocumentAsync(
        long documentId,
        IReadOnlyList<LexicalDocument> documents,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            _writer.DeleteDocuments(BuildDocumentIdTerm(documentId));

            foreach (var document in documents)
            {
                _writer.AddDocument(ToLuceneDocument(document));
            }

            Commit();

            _logger.LogInformation(
                "Indexed {ChunkCount} lexical chunks for document {DocumentId}.",
                documents.Count,
                documentId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteDocumentAsync(long documentId, CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            _writer.DeleteDocuments(BuildDocumentIdTerm(documentId));
            Commit();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SetActiveAsync(long documentId, bool isActive, CancellationToken cancellationToken = default)
    {
        // Lucene has no partial update, so the affected chunks are read back and rewritten.
        var existing = ReadDocumentChunks(documentId);
        if (existing.Count == 0)
        {
            return;
        }

        foreach (var document in existing)
        {
            document.IsActive = isActive;
        }

        await ReplaceDocumentAsync(documentId, existing, cancellationToken);
    }

    public async Task ReplaceAllAsync(
        IReadOnlyList<LexicalDocument> documents,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            _writer.DeleteAll();

            foreach (var document in documents)
            {
                _writer.AddDocument(ToLuceneDocument(document));
            }

            Commit();

            _logger.LogInformation("Rebuilt lexical index with {ChunkCount} chunks.", documents.Count);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_writer.NumDocs);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _searcherManager.Dispose();
        _writer.Dispose();
        _directory.Dispose();
        _analyzer.Dispose();
        _writeLock.Dispose();
    }

    private void Commit()
    {
        _writer.Commit();

        // Makes the just-written chunks visible to subsequent searches.
        _searcherManager.MaybeRefreshBlocking();
    }

    private List<LexicalDocument> ReadDocumentChunks(long documentId)
    {
        var searcher = _searcherManager.Acquire();

        try
        {
            var query = new TermQuery(BuildDocumentIdTerm(documentId));
            var topDocs = searcher.Search(query, int.MaxValue);
            var results = new List<LexicalDocument>(topDocs.ScoreDocs.Length);

            foreach (var scoreDoc in topDocs.ScoreDocs)
            {
                results.Add(ToLexicalDocument(searcher.Doc(scoreDoc.Doc)));
            }

            return results;
        }
        finally
        {
            _searcherManager.Release(searcher);
        }
    }

    private Similarity BuildSimilarity()
    {
        return new BM25Similarity((float)_settings.Bm25K1, (float)_settings.Bm25B);
    }

    private static Analyzer BuildAnalyzer()
    {
        // English stemming collapses "terminate"/"termination"/"terminated" onto one
        // term, which matters a great deal for policy prose. The exact-match fields
        // must not be analysed at all.
        var keyword = new KeywordAnalyzer();

        return new PerFieldAnalyzerWrapper(
            new EnglishAnalyzer(LuceneVersion.LUCENE_48),
            new Dictionary<string, Analyzer>(StringComparer.Ordinal)
            {
                [VectorPayloadFields.DocumentId] = keyword,
                [VectorPayloadFields.DocumentKey] = keyword,
                [VectorPayloadFields.Category] = keyword,
                [VectorPayloadFields.IsActive] = keyword
            });
    }

    private static Document ToLuceneDocument(LexicalDocument source)
    {
        return new Document
        {
            new StringField(VectorPayloadFields.DocumentId, source.DocumentId.ToString(), Field.Store.YES),
            new StringField(
                VectorPayloadFields.DocumentKey,
                VectorPayloadFields.BuildDocumentKey(source.DocumentId, source.ChunkIndex),
                Field.Store.YES),
            new StringField(VectorPayloadFields.Category, source.Category, Field.Store.YES),
            new StringField(
                VectorPayloadFields.IsActive,
                source.IsActive ? "true" : "false",
                Field.Store.YES),
            new TextField(VectorPayloadFields.Title, source.Title, Field.Store.YES),
            new TextField(VectorPayloadFields.SectionPath, source.SectionPath, Field.Store.YES),
            new TextField(VectorPayloadFields.Content, source.Content, Field.Store.YES),
            new StoredField(VectorPayloadFields.ChunkIndex, source.ChunkIndex),
            new StoredField(VectorPayloadFields.StartOffset, source.StartOffset),
            new StoredField(VectorPayloadFields.EndOffset, source.EndOffset)
        };
    }

    private static LexicalDocument ToLexicalDocument(Document document)
    {
        return new LexicalDocument
        {
            DocumentId = ReadInt64(document, VectorPayloadFields.DocumentId),
            ChunkIndex = ReadInt32(document, VectorPayloadFields.ChunkIndex),
            Category = document.Get(VectorPayloadFields.Category) ?? string.Empty,
            Title = document.Get(VectorPayloadFields.Title) ?? string.Empty,
            Content = document.Get(VectorPayloadFields.Content) ?? string.Empty,
            SectionPath = document.Get(VectorPayloadFields.SectionPath) ?? string.Empty,
            StartOffset = ReadInt32(document, VectorPayloadFields.StartOffset),
            EndOffset = ReadInt32(document, VectorPayloadFields.EndOffset),
            IsActive = string.Equals(document.Get(VectorPayloadFields.IsActive), "true", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static RetrievalCandidate ToCandidate(Document document)
    {
        var documentId = ReadInt64(document, VectorPayloadFields.DocumentId);
        var chunkIndex = ReadInt32(document, VectorPayloadFields.ChunkIndex);

        return new RetrievalCandidate
        {
            DocumentKey = document.Get(VectorPayloadFields.DocumentKey)
                ?? VectorPayloadFields.BuildDocumentKey(documentId, chunkIndex),
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Category = document.Get(VectorPayloadFields.Category) ?? string.Empty,
            Title = document.Get(VectorPayloadFields.Title) ?? string.Empty,
            Content = document.Get(VectorPayloadFields.Content) ?? string.Empty,
            SectionPath = document.Get(VectorPayloadFields.SectionPath) ?? string.Empty
        };
    }

    private static Term BuildDocumentIdTerm(long documentId)
    {
        return new Term(VectorPayloadFields.DocumentId, documentId.ToString());
    }

    private static long ReadInt64(Document document, string field)
    {
        var value = document.Get(field);
        return long.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static int ReadInt32(Document document, string field)
    {
        var value = document.Get(field);
        return int.TryParse(value, out var parsed) ? parsed : 0;
    }

    private static string ResolveIndexPath(string configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? RetrievalDefaults.LuceneIndexPath
            : configuredPath;

        return Path.IsPathRooted(path)
            ? path
            : Path.Combine(System.IO.Directory.GetCurrentDirectory(), path);
    }
}
