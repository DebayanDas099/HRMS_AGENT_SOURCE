using HRMS_CHATBOT_SOURCE.RAG.Models;

namespace HRMS_CHATBOT_SOURCE.RAG.Abstractions;

/// <summary>
/// Retrieval contract for the Knowledge Agent. Backed by hybrid dense + BM25
/// retrieval with rank fusion and optional reranking.
/// </summary>
public interface IVectorSearchService
{
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        string? category = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Full retrieval surface: filters, per-request stage toggles, and the
    /// confidence signal the Knowledge Agent needs before answering.
    /// </summary>
    Task<RetrievalResponse> SearchAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken = default);
}
