using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Helpers;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Logic.Common;
using HRMS_CHATBOT_SOURCE.Logic.Adapter;
using HRMS_CHATBOT_SOURCE.Repo.Document;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Logic;

public class DocumentLogic : IDocumentLogic
{
    private static readonly byte[] TokenSalt = [0x49, 0x76, 0x61, 0x6E, 0x20, 0x4D, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76];
    private static readonly HashSet<string> AllowedExtensions = new(
        DocumentUploadConstants.AllowedExtensions,
        StringComparer.OrdinalIgnoreCase);
    private static readonly AsyncLocal<ChatTurnContext?> ChatContext = new();

    private readonly IDocumentRepo _documentRepo;
    private readonly IDocumentBlobService _documentBlobService;
    private readonly IDocumentIngestionPipeline? _documentIngestionPipeline;
    private readonly ICommonLogic? _commonLogic;
    private readonly ILogger<DocumentLogic>? _logger;
    private readonly AppSettings _appSettings;

    public DocumentLogic(
        IDocumentRepo documentRepo,
        IDocumentBlobService documentBlobService,
        IDocumentIngestionPipeline? documentIngestionPipeline = null,
        ICommonLogic? commonLogic = null,
        ILogger<DocumentLogic>? logger = null,
        IOptions<AppSettings>? appSettings = null)
    {
        _documentRepo = documentRepo;
        _documentBlobService = documentBlobService;
        _documentIngestionPipeline = documentIngestionPipeline;
        _commonLogic = commonLogic;
        _logger = logger;
        _appSettings = appSettings?.Value ?? new AppSettings();
    }

    public void SetChatTurnContext(string? mobile, string? baseUrl)
    {
        ChatContext.Value = new ChatTurnContext(mobile?.Trim(), baseUrl?.Trim());
    }

    public void ClearChatTurnContext()
    {
        ChatContext.Value = null;
    }

    public async Task<DocumentStatisticsDto?> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _documentRepo.GetStatisticsAsync(cancellationToken);
        return DocumentAdapter.MapStatistics(response);
    }

    public async Task<DocumentListResponseDto?> GetDocumentsAsync(
        string? category,
        string? searchText,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        ValidateCategory(category, required: false);

        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => pageSize
        };

        var response = await _documentRepo.GetListAsync(category, searchText, pageNumber, pageSize, cancellationToken);
        return DocumentAdapter.MapPagedList(response, pageNumber, pageSize);
    }

    public async Task<BulkDocumentUploadResponse?> BulkUploadAsync(
        string? category,
        IReadOnlyList<IFormFile> files,
        string? title,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        ValidateCategory(category, required: true);

        if (files == null || files.Count == 0)
        {
            throw new ValidationException("Select at least one file to upload.");
        }

        if (string.IsNullOrWhiteSpace(createdBy))
        {
            throw new ValidationException("User context is required for upload.");
        }

        var response = new BulkDocumentUploadResponse
        {
            TotalFiles = files.Count
        };

        foreach (var file in files)
        {
            var result = new DocumentUploadResultItem
            {
                FileName = file.FileName
            };

            try
            {
                ValidateFile(file);
                ValidateTitle(title);

                var documentTitle = title!.Trim();

                await using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                var fileData = stream.ToArray();

                var blobPath = await _documentBlobService.UploadAsync(
                    category!,
                    file.FileName,
                    fileData,
                    cancellationToken);

                var insertResponse = await _documentRepo.InsertAsync(
                    category,
                    documentTitle,
                    blobPath,
                    createdBy,
                    cancellationToken);

                var documentId = DocumentAdapter.MapInsertedDocumentId(insertResponse);
                result.Document = new DocumentMstrDto
                {
                    DocumentId = documentId,
                    Category = category!,
                    Name = documentTitle,
                    Path = blobPath,
                    CreatedBy = createdBy,
                    CreatedDate = DateTime.Now,
                    Active = "Y"
                };

                result.Success = true;
                result.Message = "Uploaded successfully.";
                response.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message;
                response.FailedCount++;
            }

            response.Results.Add(result);
        }

        return response;
    }

    public async Task<DocumentMstrDto?> UpdateActiveAsync(
        long documentId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            throw new ValidationException("Invalid document id.");
        }

        var response = await _documentRepo.UpdateActiveAsync(documentId, isActive ? "Y" : "N", cancellationToken);
        DocumentAdapter.EnsureSuccess(response);

        if (_documentIngestionPipeline != null)
        {
            try
            {
                // Best-effort, like the blob delete below: the database is the record of
                // truth for active state, and a search-index sync failure is repairable.
                await _documentIngestionPipeline.SetDocumentActiveAsync(documentId, isActive, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Failed to sync active state to the search indexes for document {DocumentId}.",
                    documentId);
            }
        }

        return new DocumentMstrDto
        {
            DocumentId = documentId,
            Active = isActive ? "Y" : "N"
        };
    }

    public async Task<IngestionResultDto?> IngestDocumentAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            throw new ValidationException("Invalid document id.");
        }

        if (_documentIngestionPipeline == null)
        {
            throw new ValidationException("Document ingestion pipeline is not configured.");
        }

        return await _documentIngestionPipeline.IngestAsync(documentId, cancellationToken);
    }

    public async Task<DeleteDocumentResponse?> DeleteDocumentAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            throw new ValidationException("Invalid document id.");
        }

        var response = await _documentRepo.GetByIdAsync(documentId, cancellationToken);
        var document = DocumentAdapter.MapById(response);
        if (document == null)
        {
            throw new ValidationException("Document not found.");
        }

        var vectorsDeleted = false;
        if (_documentIngestionPipeline != null)
        {
            vectorsDeleted = await _documentIngestionPipeline.RemoveFromIndexAsync(documentId, cancellationToken);
        }

        var blobDeleted = false;
        if (!string.IsNullOrWhiteSpace(document.Path))
        {
            try
            {
                await _documentBlobService.DeleteAsync(document.Path, cancellationToken);
                blobDeleted = true;
            }
            catch (Exception ex)
            {
                // A missing or already-removed blob must not block deleting the record.
                _logger?.LogWarning(ex, "Blob delete failed for document {DocumentId} at {Path}.", documentId, document.Path);
            }
        }

        var deleteResponse = await _documentRepo.DeleteAsync(documentId, cancellationToken);
        DocumentAdapter.EnsureSuccess(deleteResponse);

        return new DeleteDocumentResponse
        {
            DocumentId = documentId,
            BlobDeleted = blobDeleted,
            VectorsDeleted = vectorsDeleted,
            Message = "Document deleted successfully."
        };
    }

    public async Task<DocumentDownloadResult?> DownloadDocumentAsync(
        long documentId,
        CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            throw new ValidationException("Invalid document id.");
        }

        var response = await _documentRepo.GetByIdAsync(documentId, cancellationToken);
        var document = DocumentAdapter.MapById(response);
        if (document == null || string.IsNullOrWhiteSpace(document.Path))
        {
            throw new ValidationException("Document not found.");
        }

        var fileName = DocumentFileHelper.ExtractFileName(document.Path);
        var fileContent = await _documentBlobService.DownloadAsync(document.Path, cancellationToken);

        return new DocumentDownloadResult
        {
            FileName = fileName,
            ContentType = DocumentFileHelper.ResolveMimeType(fileName),
            FileContent = fileContent
        };
    }

    public async Task<IReadOnlyList<DocumentSimilarityMatchDto>> GetDocumentMatchesBySimilarityAsync(
        string searchText,
        int minScore = 65,
        int topCount = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        var normalizedScore = minScore switch
        {
            < 0 => 0,
            > 100 => 100,
            _ => minScore
        };
        var normalizedTopCount = topCount switch
        {
            < 1 => 1,
            > 10 => 10,
            _ => topCount
        };

        var response = await _documentRepo.GetBySimilarityAsync(
            searchText.Trim(),
            normalizedScore,
            normalizedTopCount,
            cancellationToken);
        return DocumentAdapter.MapSimilarityMatches(response);
    }

    public string BuildDocumentDownloadLink(long documentId)
    {
        if (documentId <= 0)
        {
            return "Unable to generate secure download link.";
        }

        var baseUrl = ChatContext.Value?.BaseUrl;
        var mobile = ChatContext.Value?.Mobile;
        var token = EncryptDownloadToken(mobile, documentId, _appSettings.EncryptionKey);
        if (string.IsNullOrWhiteSpace(token))
        {
            return "Unable to generate secure download link.";
        }

        var relativeUrl = $"/api/ChatDocumentDownload?token={token}";
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return relativeUrl;
        }

        var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
        return Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out _)
            ? normalizedBaseUrl + relativeUrl
            : relativeUrl;
    }

    public bool TryDecodeDocumentDownloadToken(string? token, out long documentId)
    {
        documentId = 0;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var decryptedJson = DecryptDownloadToken(token, _appSettings.EncryptionKey);
        if (string.IsNullOrWhiteSpace(decryptedJson))
        {
            return false;
        }

        TokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TokenPayload>(decryptedJson);
        }
        catch
        {
            return false;
        }

        if (payload == null || payload.DocumentId <= 0)
        {
            return false;
        }

        documentId = payload.DocumentId;
        return true;
    }

    public async Task<string> SendDocumentLinkByMailAsync(
        long documentId,
        string? documentName = null,
        CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            return "Invalid document id.";
        }

        if (_commonLogic == null)
        {
            return "Mail service is not available right now.";
        }

        var mobile = ChatContext.Value?.Mobile;
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return "Unable to identify your registered mobile number for this chat.";
        }

        var recipient = await _commonLogic
            .GetUserEmailByMobileAsync(mobile, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return $"No active email id found for mobile {mobile}.";
        }

        var downloadLink = BuildDocumentDownloadLink(documentId);
        if (downloadLink.StartsWith("Unable to generate", StringComparison.OrdinalIgnoreCase))
        {
            return "Unable to generate download link for email.";
        }

        var safeName = string.IsNullOrWhiteSpace(documentName)
            ? $"Document #{documentId}"
            : documentName.Trim();
        var subject = $"HRMS Document Link - {safeName}";
        var body =
            "<p>Dear User,</p>"
            + "<p>"
            + $"As requested, please find the <b>{safeName}</b> document available for your reference."
            + "</p>"
            + "<p>"
            + "You can download the document by clicking the button below:"
            + "</p>"
            + "<p>"
            + $"<a href=\"{downloadLink}\" "
            + "style=\"display:inline-block;padding:10px 22px;background:#1a73e8;color:#ffffff;text-decoration:none;border-radius:5px;font-weight:bold;\">"
            + $"Download {safeName}"
            + "</a>"
            + "</p>"
            + "<p>"
            + "If you need any further assistance, please contact the HRMS Team."
            + "</p>"
            + "<p>"
            + "Regards,<br/>"
            + "<b>HRMS Team</b>"
            + "</p>";

        var responseCode = await _commonLogic
            .SendMailNewAsync(recipient, subject, body, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return responseCode == 1
            ? $"Mail sent successfully to {recipient}."
            : $"Failed to send mail to {recipient}.";
    }

    private static void ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ValidationException("Document title is required.");
        }

        if (title.Trim().Length > 500)
        {
            throw new ValidationException("Document title cannot exceed 500 characters.");
        }
    }

    private static void ValidateCategory(string? category, bool required)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            if (required)
            {
                throw new ValidationException("Document category is required.");
            }

            return;
        }

        if (!DocumentCategories.Allowed.Any(value =>
                string.Equals(value, category, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException("Invalid document category. Allowed values: Policy, Training.");
        }
    }

    private static void ValidateFile(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new ValidationException($"File '{file.FileName}' is empty.");
        }

        if (file.Length > DocumentUploadConstants.MaxFileSizeBytes)
        {
            throw new ValidationException($"File '{file.FileName}' exceeds the 200 MB limit.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ValidationException($"File type '{extension}' is not allowed for '{file.FileName}'.");
        }
    }

    private static string EncryptDownloadToken(string? mobile, long documentId, string? encryptionKey)
    {
        if (documentId <= 0 || string.IsNullOrWhiteSpace(encryptionKey))
        {
            return string.Empty;
        }

        try
        {
            var payloadJson = JsonSerializer.Serialize(new TokenPayload(mobile ?? string.Empty, documentId));
            var clearBytes = Encoding.UTF8.GetBytes(payloadJson);

            using var aes = Aes.Create();
            using var pdb = new Rfc2898DeriveBytes(encryptionKey, TokenSalt, 1000, HashAlgorithmName.SHA256);
            aes.Key = pdb.GetBytes(32);
            aes.IV = pdb.GetBytes(16);

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(clearBytes, 0, clearBytes.Length);
                cs.FlushFinalBlock();
            }

            return Uri.EscapeDataString(Convert.ToBase64String(ms.ToArray()));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string DecryptDownloadToken(string token, string? encryptionKey)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(encryptionKey))
        {
            return string.Empty;
        }

        try
        {
            var normalized = Uri.UnescapeDataString(token).Replace(" ", "+", StringComparison.Ordinal);
            var cipherBytes = Convert.FromBase64String(normalized);

            using var aes = Aes.Create();
            using var pdb = new Rfc2898DeriveBytes(encryptionKey, TokenSalt, 1000, HashAlgorithmName.SHA256);
            aes.Key = pdb.GetBytes(32);
            aes.IV = pdb.GetBytes(16);

            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cs.Write(cipherBytes, 0, cipherBytes.Length);
                cs.FlushFinalBlock();
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed record ChatTurnContext(string? Mobile, string? BaseUrl);
    private sealed record TokenPayload(string Mobile, long DocumentId);
}
