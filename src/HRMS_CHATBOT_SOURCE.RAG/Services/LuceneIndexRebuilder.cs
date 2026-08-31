using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using HRMS_CHATBOT_SOURCE.Repo.Document;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.RAG.Services;

/// <summary>
/// Reconstructs the Lucene index from Qdrant payloads, which remain the source of truth.
/// <para>
/// This is what makes the lexical index disposable: it covers first deployment over an
/// already-populated collection, recovery from a lost or corrupt index directory, and
/// repair after a partial ingestion where the vector write succeeded but the lexical one
/// did not.
/// </para>
/// </summary>
public class LuceneIndexRebuilder
{
    private static readonly SemaphoreSlim RebuildLock = new(1, 1);
    private static volatile bool _startupCheckCompleted;

    private readonly IVectorStoreService _vectorStoreService;
    private readonly ILexicalIndex _lexicalIndex;
    private readonly IDocumentRepo _documentRepo;
    private readonly RetrievalSettings _settings;
    private readonly ILogger<LuceneIndexRebuilder> _logger;

    public LuceneIndexRebuilder(
        IVectorStoreService vectorStoreService,
        ILexicalIndex lexicalIndex,
        IDocumentRepo documentRepo,
        IOptions<RetrievalSettings> settings,
        ILogger<LuceneIndexRebuilder> logger)
    {
        _vectorStoreService = vectorStoreService;
        _lexicalIndex = lexicalIndex;
        _documentRepo = documentRepo;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Rebuilds once, lazily, when the index is empty but the collection has data.
    /// Deliberately not done at startup: on a large corpus that would delay readiness
    /// for a service that may never receive a query.
    /// </summary>
    public async Task EnsureIndexPopulatedAsync(CancellationToken cancellationToken = default)
    {
        if (_startupCheckCompleted || !_settings.AutoRebuildOnEmptyIndex)
        {
            return;
        }

        await RebuildLock.WaitAsync(cancellationToken);

        try
        {
            if (_startupCheckCompleted)
            {
                return;
            }

            var indexed = await _lexicalIndex.CountAsync(cancellationToken);
            if (indexed > 0)
            {
                _startupCheckCompleted = true;
                return;
            }

            _logger.LogInformation("Lexical index is empty; rebuilding from the vector store.");
            await RebuildCoreAsync(cancellationToken);
            _startupCheckCompleted = true;
        }
        catch (Exception ex)
        {
            // Retrieval still works dense-only, so a failed rebuild must not fail the query.
            _logger.LogError(ex, "Automatic lexical index rebuild failed; continuing without it.");
            _startupCheckCompleted = true;
        }
        finally
        {
            RebuildLock.Release();
        }
    }

    public async Task<int> RebuildAsync(CancellationToken cancellationToken = default)
    {
        await RebuildLock.WaitAsync(cancellationToken);

        try
        {
            var count = await RebuildCoreAsync(cancellationToken);
            _startupCheckCompleted = true;
            return count;
        }
        finally
        {
            RebuildLock.Release();
        }
    }

    private async Task<int> RebuildCoreAsync(CancellationToken cancellationToken)
    {
        var documents = await _vectorStoreService.ScrollAllAsync(cancellationToken);

        if (documents.Count == 0)
        {
            await _lexicalIndex.ReplaceAllAsync([], cancellationToken);
            return 0;
        }

        // Existing payloads predate is_active maintenance, so SQL is authoritative here.
        var inactive = await LoadInactiveDocumentIdsAsync(cancellationToken);

        foreach (var document in documents.Where(document => inactive.Contains(document.DocumentId)))
        {
            document.IsActive = false;
        }

        await _lexicalIndex.ReplaceAllAsync(documents, cancellationToken);

        _logger.LogInformation(
            "Rebuilt lexical index from {ChunkCount} vector payloads ({InactiveCount} inactive documents).",
            documents.Count,
            inactive.Count);

        return documents.Count;
    }

    private async Task<HashSet<long>> LoadInactiveDocumentIdsAsync(CancellationToken cancellationToken)
    {
        var inactive = new HashSet<long>();

        try
        {
            var response = await _documentRepo.GetListAsync(
                category: null,
                searchText: null,
                pageNumber: 1,
                pageSize: int.MaxValue,
                cancellationToken);

            if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet)
            {
                return inactive;
            }

            var table = dataSet.Tables.Count > 1 ? dataSet.Tables[1] : dataSet.Tables[0];
            if (!table.Columns.Contains("dm_id") || !table.Columns.Contains("dm_active"))
            {
                return inactive;
            }

            foreach (DataRow row in table.Rows)
            {
                var active = Convert.ToString(row["dm_active"]) ?? "Y";

                if (!string.Equals(active.Trim(), "Y", StringComparison.OrdinalIgnoreCase))
                {
                    inactive.Add(Convert.ToInt64(row["dm_id"]));
                }
            }
        }
        catch (Exception ex)
        {
            // Without this the index is merely over-inclusive, which the query-time filter narrows anyway.
            _logger.LogWarning(ex, "Could not read document active state during rebuild.");
        }

        return inactive;
    }
}
