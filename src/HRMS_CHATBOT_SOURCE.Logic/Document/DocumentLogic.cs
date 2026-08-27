using System.ComponentModel.DataAnnotations;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Helpers;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Logic.Adapter;
using HRMS_CHATBOT_SOURCE.Repo.Document;
using Microsoft.AspNetCore.Http;

namespace HRMS_CHATBOT_SOURCE.Logic;

public class DocumentLogic : IDocumentLogic
{
    private static readonly HashSet<string> AllowedExtensions = new(
        DocumentUploadConstants.AllowedExtensions,
        StringComparer.OrdinalIgnoreCase);

    private readonly IDocumentRepo _documentRepo;
    private readonly IDocumentBlobService _documentBlobService;
    private readonly IDocumentIngestionPipeline? _documentIngestionPipeline;

    public DocumentLogic(
        IDocumentRepo documentRepo,
        IDocumentBlobService documentBlobService,
        IDocumentIngestionPipeline? documentIngestionPipeline = null)
    {
        _documentRepo = documentRepo;
        _documentBlobService = documentBlobService;
        _documentIngestionPipeline = documentIngestionPipeline;
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
}
