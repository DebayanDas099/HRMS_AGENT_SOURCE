using HRMS_CHATBOT_SOURCE.Domain.Models;
using MCC.Foundation.MSSQLHelper.Models;

namespace HRMS_CHATBOT_SOURCE.Repo.Document;

public interface IDocumentRepo
{
    Task<MSSQLResponse?> GetStatisticsAsync(CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetListAsync(
        string? category,
        string? searchText,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> InsertAsync(
        string? category,
        string? name,
        string? path,
        string? createdBy,
        CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> UpdateActiveAsync(long documentId, string active, CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> DeleteAsync(long documentId, CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetByIdAsync(long documentId, CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> UpdateIngestionAsync(
        long documentId,
        string ingestionStatus,
        DateTime? ingestedAt,
        string? ingestionError,
        int? chunkCount,
        CancellationToken cancellationToken = default);
}
