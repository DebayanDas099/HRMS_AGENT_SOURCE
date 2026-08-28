using HRMS_CHATBOT_SOURCE.RAG.Models;

namespace HRMS_CHATBOT_SOURCE.RAG.Abstractions;

/// <summary>
/// Corpus-wide BM25 index over ingested chunks. Retrieves independently of the
/// dense index so exact-term queries (acronyms, form names, clause numbers) can
/// surface chunks the embedding never returns.
/// </summary>
public interface ILexicalIndex
{
    Task<IReadOnlyList<RetrievalCandidate>> SearchAsync(
        string query,
        int limit,
        string? category,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces every chunk for a document; mirrors the delete-then-insert reindex in Qdrant.</summary>
    Task ReplaceDocumentAsync(
        long documentId,
        IReadOnlyList<LexicalDocument> documents,
        CancellationToken cancellationToken = default);

    Task DeleteDocumentAsync(long documentId, CancellationToken cancellationToken = default);

    Task SetActiveAsync(long documentId, bool isActive, CancellationToken cancellationToken = default);

    Task ReplaceAllAsync(IReadOnlyList<LexicalDocument> documents, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
