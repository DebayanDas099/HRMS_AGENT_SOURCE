using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Domain.Interfaces;

public interface IDocumentBlobService
{
    Task<string> UploadAsync(string category, string fileName, byte[] fileData, CancellationToken cancellationToken = default);

    Task<byte[]> DownloadAsync(string documentPath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string documentPath, CancellationToken cancellationToken = default);
}
