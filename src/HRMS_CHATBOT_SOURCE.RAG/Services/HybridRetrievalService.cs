using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Helpers;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.RAG.Services;

/// <summary>
/// Hybrid retrieval: dense vector search and corpus-wide BM25 run independently and
/// concurrently, are combined with Reciprocal Rank Fusion, then optionally reranked.
/// <para>
/// Running BM25 over the whole corpus rather than over the dense hits is the point of
/// the design: exact-term queries (acronyms, form names, clause numbers) routinely miss
/// on embeddings alone, and a lexical stage that only sees dense candidates can never
/// recover them.
/// </para>
/// </summary>
public class HybridRetrievalService : IVectorSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ILexicalIndex _lexicalIndex;
    private readonly IRerankService _rerankService;
    private readonly LuceneIndexRebuilder _rebuilder;
    private readonly RetrievalSettings _settings;
    private readonly VectorStoreSettings _vectorStoreSettings;
    private readonly ILogger<HybridRetrievalService> _logger;

    public HybridRetrievalService(
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ILexicalIndex lexicalIndex,
        IRerankService rerankService,
        LuceneIndexRebuilder rebuilder,
        IOptions<RetrievalSettings> retrievalOptions,
        IOptions<VectorStoreSettings> vectorStoreOptions,
        ILogger<HybridRetrievalService> logger)
    {
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _lexicalIndex = lexicalIndex;
        _rerankService = rerankService;
        _rebuilder = rebuilder;
        _settings = retrievalOptions.Value;
        _vectorStoreSettings = vectorStoreOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string query,
        int topK = RetrievalDefaults.TopK,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SearchAsync(
            new RetrievalQuery
            {
                Query = query,
                TopK = topK,
                Category = category
            },
            cancellationToken);

        return response.Results;
    }

    public async Task<RetrievalResponse> SearchAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null || string.IsNullOrWhiteSpace(query.Query))
        {
            throw new ValidationException("Search query is required.");
        }

        if (!_settings.Enabled)
        {
            throw new ValidationException("Retrieval is disabled in configuration.");
        }

        ValidateCategory(query.Category);

        var topK = Math.Clamp(query.TopK <= 0 ? _settings.TopK : query.TopK, 1, RetrievalDefaults.MaxTopK);
        var useLexical = _settings.LexicalEnabled && query.EnableLexical && _settings.SparseCandidates > 0;
        var stopwatch = Stopwatch.StartNew();

        if (useLexical)
        {
            await _rebuilder.EnsureIndexPopulatedAsync(cancellationToken);
        }

        var queryVector = await EmbedQueryAsync(query.Query, cancellationToken);

        var denseTask = _vectorStoreService.SearchAsync(
            queryVector,
            _settings.DenseCandidates,
            query.Category,
            query.ActiveOnly,
            cancellationToken);

        var sparseTask = useLexical
            ? _lexicalIndex.SearchAsync(
                query.Query,
                _settings.SparseCandidates,
                query.Category,
                query.ActiveOnly,
                cancellationToken)
            : Task.FromResult<IReadOnlyList<RetrievalCandidate>>([]);

        await Task.WhenAll(denseTask, sparseTask);

        var dense = denseTask.Result;
        var sparse = sparseTask.Result;

        var fused = RrfFusion.Fuse(dense, sparse, _settings.RrfK);
        var fusedCount = fused.Count;

        var useRerank = _settings.RerankEnabled && query.EnableRerank && fused.Count > 1;
        var ranked = fused.Take(Math.Max(topK, _settings.RerankCandidates)).ToList();

        if (useRerank)
        {
            ranked = [.. await _rerankService.RerankAsync(query.Query, ranked, cancellationToken)];
        }

        var top = ranked.Take(topK).ToList();
        var confidence = CalculateConfidence(top, useRerank);

        stopwatch.Stop();

        _logger.LogInformation(
            "Retrieval for {Query}: dense={DenseCount} sparse={SparseCount} fused={FusedCount} "
            + "returned={ReturnedCount} reranked={UsedRerank} confidence={Confidence:F2} in {ElapsedMs}ms.",
            query.Query,
            dense.Count,
            sparse.Count,
            fusedCount,
            top.Count,
            useRerank,
            confidence,
            stopwatch.ElapsedMilliseconds);

        return new RetrievalResponse
        {
            Query = query.Query,
            Results = top.Select(ToResult).ToList(),
            Confidence = confidence,
            IsConfident = confidence >= _settings.MinimumConfidence,
            UsedRerank = useRerank,
            UsedLexical = useLexical,
            DenseCount = dense.Count,
            SparseCount = sparse.Count,
            FusedCount = fusedCount,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
        };
    }

    private async Task<float[]> EmbedQueryAsync(string query, CancellationToken cancellationToken)
    {
        var embeddings = await _embeddingService.CreateEmbeddingsAsync([query], cancellationToken);

        if (embeddings.Count == 0)
        {
            throw new ValidationException("Could not create an embedding for the query.");
        }

        var vector = embeddings[0];

        // Caught here rather than at the Qdrant boundary, where it surfaces as an opaque gRPC error.
        if (vector.Length != _vectorStoreSettings.VectorSize)
        {
            throw new ValidationException(
                $"Query embedding has {vector.Length} dimensions but the collection expects "
                + $"{_vectorStoreSettings.VectorSize}. Check the configured embedding deployment.");
        }

        return vector;
    }

    /// <summary>
    /// Fusion scores are rank-derived and have no absolute meaning, so they are never
    /// reported as confidence. Reranker relevance and cosine similarity both do.
    /// </summary>
    private static double CalculateConfidence(IReadOnlyList<RetrievalCandidate> results, bool usedRerank)
    {
        if (results.Count == 0)
        {
            return 0;
        }

        var top = results[0];

        if (usedRerank && top.RerankScore.HasValue)
        {
            return Math.Clamp(top.RerankScore.Value / 10.0, 0, 1);
        }

        if (top.DenseScore.HasValue)
        {
            return Math.Clamp(top.DenseScore.Value, 0, 1);
        }

        // Lexical-only hit: BM25 is unbounded, so report a deliberately conservative floor.
        return top.SparseRank.HasValue ? 0.5 : 0;
    }

    private static VectorSearchResult ToResult(RetrievalCandidate candidate)
    {
        return new VectorSearchResult
        {
            DocumentId = candidate.DocumentId,
            ChunkIndex = candidate.ChunkIndex,
            Category = candidate.Category,
            Title = candidate.Title,
            Content = candidate.Content,
            SectionPath = candidate.SectionPath,
            Score = (float)candidate.FinalScore,
            MatchedBy = DescribeMatch(candidate)
        };
    }

    private static string DescribeMatch(RetrievalCandidate candidate)
    {
        if (candidate.DenseRank.HasValue && candidate.SparseRank.HasValue)
        {
            return "hybrid";
        }

        return candidate.DenseRank.HasValue ? "dense" : "lexical";
    }

    private static void ValidateCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return;
        }

        if (!DocumentCategories.Allowed.Any(value =>
                string.Equals(value, category, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("Invalid document category. Allowed values: Policy, Training.");
        }
    }
}
