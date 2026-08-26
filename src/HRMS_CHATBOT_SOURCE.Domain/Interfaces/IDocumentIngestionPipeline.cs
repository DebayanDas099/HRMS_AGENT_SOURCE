using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Domain.Interfaces;

public interface IDocumentIngestionPipeline
{
    Task<IngestionResultDto> IngestAsync(long documentId, CancellationToken cancellationToken = default);
}
