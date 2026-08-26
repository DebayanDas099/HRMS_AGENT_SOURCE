using HRMS_CHATBOT_SOURCE.RAG.Models;

namespace HRMS_CHATBOT_SOURCE.RAG.Abstractions;

public interface IVectorStoreService
{
    Task EnsureCollectionAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(
        IReadOnlyList<VectorDocumentPoint> points,
        CancellationToken cancellationToken = default);

    Task DeleteByDocumentIdAsync(long documentId, CancellationToken cancellationToken = default);
}
