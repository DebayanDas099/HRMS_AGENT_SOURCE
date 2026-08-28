using HRMS_CHATBOT_SOURCE.RAG.Models;

namespace HRMS_CHATBOT_SOURCE.RAG.Abstractions;

public interface IRerankService
{
    /// <summary>
    /// Scores candidates against the query. Implementations must degrade to the
    /// input order on any failure rather than throwing: a reranker that can take
    /// retrieval down is worse than no reranker.
    /// </summary>
    Task<IReadOnlyList<RetrievalCandidate>> RerankAsync(
        string query,
        IReadOnlyList<RetrievalCandidate> candidates,
        CancellationToken cancellationToken = default);
}
