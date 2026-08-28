using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using Microsoft.AspNetCore.Http;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface IDocumentLogic
{
    Task<DocumentStatisticsDto?> GetStatisticsAsync(CancellationToken cancellationToken = default);

    Task<DocumentListResponseDto?> GetDocumentsAsync(
        string? category,
        string? searchText,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<BulkDocumentUploadResponse?> BulkUploadAsync(
        string? category,
        IReadOnlyList<IFormFile> files,
        string? title,
        string? createdBy,
        CancellationToken cancellationToken = default);

    Task<DocumentMstrDto?> UpdateActiveAsync(long documentId, bool isActive, CancellationToken cancellationToken = default);

    Task<IngestionResultDto?> IngestDocumentAsync(long documentId, CancellationToken cancellationToken = default);

    Task<DeleteDocumentResponse?> DeleteDocumentAsync(long documentId, CancellationToken cancellationToken = default);

    Task<DocumentDownloadResult?> DownloadDocumentAsync(long documentId, CancellationToken cancellationToken = default);
}
