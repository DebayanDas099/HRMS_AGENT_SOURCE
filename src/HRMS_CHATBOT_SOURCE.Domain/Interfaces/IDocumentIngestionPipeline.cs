using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Domain.Interfaces;

public interface IDocumentIngestionPipeline
{
    Task<IngestionResultDto> IngestAsync(long documentId, CancellationToken cancellationToken = default);

    Task<bool> RemoveFromIndexAsync(long documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs a document's active state into the search indexes. Without this,
    /// deactivating a document only blocks re-ingestion and leaves it retrievable.
    /// </summary>
    Task SetDocumentActiveAsync(long documentId, bool isActive, CancellationToken cancellationToken = default);
}
