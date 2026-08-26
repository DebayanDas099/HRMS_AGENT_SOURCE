using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using MCC.Foundation.StorageManager.StorageManagement;
using MCC.Foundation.StorageManager.StorageManagement.Azure;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Domain.Services;

public class DocumentBlobService : IDocumentBlobService
{
    private readonly IStorageService _storageService;
    private readonly StorageSettings _storageSettings;

    public DocumentBlobService(IStorageService storageService, IOptions<StorageSettings> storageSettings)
    {
        _storageService = storageService;
        _storageSettings = storageSettings.Value;
    }

    public async Task<string> UploadAsync(
        string category,
        string fileName,
        byte[] fileData,
        CancellationToken cancellationToken = default)
    {
        var credential = BuildCredential();
        var client = _storageService.GetClient(credential);
        var containerName = _storageSettings.BlobContainer;
        var blobName = BuildBlobName(category, fileName);

        if (!await _storageService.ContainerExistsAsync(client, containerName))
        {
            await _storageService.CreateContainerAsync(client, containerName);
        }

        await _storageService.UploadFileAsync(client, containerName, fileData, blobName, overWrite: false);
        return $"{containerName}/{blobName}";
    }

    public async Task DeleteAsync(string documentPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return;
        }

        var parts = documentPath.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return;
        }

        var credential = BuildCredential();
        var client = _storageService.GetClient(credential);
        await _storageService.DeleteFileAsync(client, parts[0], parts[1]);
    }

    public async Task<byte[]> DownloadAsync(string documentPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            throw new InvalidOperationException("Document path is required.");
        }

        var parts = documentPath.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Invalid document path '{documentPath}'.");
        }

        var credential = BuildCredential();
        var client = _storageService.GetClient(credential);
        return await _storageService.GetFileAsync(client, parts[0], parts[1]);
    }

    private AzureStorageCredential BuildCredential()
    {
        var connectionString = _storageSettings.ConnectionString;
        var accountKey = _storageSettings.AccountKey ?? ExtractAccountKey(connectionString);

        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(accountKey))
        {
            throw new InvalidOperationException("Storage:ConnectionString and Storage:AccountKey must be configured for blob upload.");
        }

        return new AzureStorageCredential
        {
            ConnectionString = connectionString,
            StorageAccountKey = accountKey
        };
    }

    private static string BuildBlobName(string category, string fileName)
    {
        var safeCategory = category.Trim().Replace(' ', '-');
        var safeFileName = Path.GetFileName(fileName);
        return $"{safeCategory}/{Guid.NewGuid():N}_{safeFileName}";
    }

    private static string? ExtractAccountKey(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var segments = part.Split('=', 2);
            if (segments.Length == 2
                && string.Equals(segments[0].Trim(), "AccountKey", StringComparison.OrdinalIgnoreCase))
            {
                return segments[1].Trim();
            }
        }

        return null;
    }
}
