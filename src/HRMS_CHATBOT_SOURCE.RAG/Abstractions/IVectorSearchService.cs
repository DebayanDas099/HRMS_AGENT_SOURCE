using HRMS_CHATBOT_SOURCE.RAG.Models;

namespace HRMS_CHATBOT_SOURCE.RAG.Abstractions;

/// <summary>
/// Retrieval contract for the Knowledge Agent. Implementation will be added
/// when agent execution is wired to Qdrant search.
/// </summary>
public interface IVectorSearchService
{
    Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        string query,
        int topK = 5,
        string? category = null,
        CancellationToken cancellationToken = default);
}
