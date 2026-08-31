using HRMS_CHATBOT_SOURCE.RAG.Models;

namespace HRMS_CHATBOT_SOURCE.RAG.Abstractions;

public interface IVectorStoreService
{
    Task EnsureCollectionAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(
        IReadOnlyList<VectorDocumentPoint> points,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByDocumentIdAsync(long documentId, CancellationToken cancellationToken = default);

    Task DeleteByDocumentIdAsync(long documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrievalCandidate>> SearchAsync(
        float[] queryVector,
        int limit,
        string? category,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    /// <summary>Updates the is_active payload for every chunk of a document.</summary>
    Task SetActiveAsync(long documentId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Streams the whole collection's payloads; used to rebuild the lexical index.</summary>
    Task<IReadOnlyList<LexicalDocument>> ScrollAllAsync(CancellationToken cancellationToken = default);
}
