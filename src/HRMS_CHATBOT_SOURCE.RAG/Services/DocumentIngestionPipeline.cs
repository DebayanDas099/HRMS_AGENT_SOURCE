using System.ComponentModel.DataAnnotations;
using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using HRMS_CHATBOT_SOURCE.Repo.Document;
using MCC.Foundation.Chunker.Abstractions;
using MCC.Foundation.Chunker.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.RAG.Services;

public class DocumentIngestionPipeline : IDocumentIngestionPipeline
{
    private readonly IDocumentRepo _documentRepo;
    private readonly IDocumentBlobService _documentBlobService;
    private readonly IDocumentChunkerService _documentChunkerService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly IngestionSettings _ingestionSettings;
    private readonly VectorStoreSettings _vectorStoreSettings;
    private readonly ILogger<DocumentIngestionPipeline> _logger;

    public DocumentIngestionPipeline(
        IDocumentRepo documentRepo,
        IDocumentBlobService documentBlobService,
        IDocumentChunkerService documentChunkerService,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        IOptions<IngestionSettings> ingestionOptions,
        IOptions<VectorStoreSettings> vectorStoreOptions,
        ILogger<DocumentIngestionPipeline> logger)
    {
        _documentRepo = documentRepo;
        _documentBlobService = documentBlobService;
        _documentChunkerService = documentChunkerService;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _ingestionSettings = ingestionOptions.Value;
        _vectorStoreSettings = vectorStoreOptions.Value;
        _logger = logger;
    }

    public async Task<IngestionResultDto> IngestAsync(long documentId, CancellationToken cancellationToken = default)
    {
        if (!_ingestionSettings.Enabled)
        {
            return new IngestionResultDto
            {
                DocumentId = documentId,
                Status = IngestionStatuses.Skipped,
                ErrorMessage = "Ingestion is disabled in configuration."
            };
        }

        var document = MapDocument(await _documentRepo.GetByIdAsync(documentId, cancellationToken));
        if (document == null)
        {
            throw new ValidationException("Document not found.");
        }

        if (!string.Equals(document.Active, "Y", StringComparison.OrdinalIgnoreCase))
        {
            return await CompleteAsync(
                documentId,
                IngestionStatuses.Skipped,
                0,
                0,
                "Inactive documents are not ingested.",
                cancellationToken);
        }

        await UpdateStatusAsync(documentId, IngestionStatuses.Processing, null, null, null, cancellationToken);

        try
        {
            var fileName = ExtractFileName(document.Path);
            var fileBytes = await _documentBlobService.DownloadAsync(document.Path, cancellationToken);

            await using var contentStream = new MemoryStream(fileBytes);
            var chunkRequest = new DocumentChunkRequest
            {
                FileName = fileName,
                Content = contentStream,
                MimeType = ResolveMimeType(fileName)
            };

            var chunks = await _documentChunkerService.ChunkDocumentAsync(chunkRequest, cancellationToken);
            if (chunks == null || chunks.Count == 0)
            {
                return await CompleteAsync(
                    documentId,
                    IngestionStatuses.Failed,
                    0,
                    0,
                    "No chunks were produced from the uploaded document.",
                    cancellationToken);
            }

            var chunkTexts = chunks
                .Select(chunk => chunk.Content?.Trim())
                .Where(content => !string.IsNullOrWhiteSpace(content))
                .Cast<string>()
                .ToList();

            if (chunkTexts.Count == 0)
            {
                return await CompleteAsync(
                    documentId,
                    IngestionStatuses.Failed,
                    0,
                    0,
                    "Document chunks did not contain searchable text.",
                    cancellationToken);
            }

            bool isDocPresent = await _vectorStoreService.ExistsByDocumentIdAsync(documentId, cancellationToken);
            if (isDocPresent)
            {
                await _vectorStoreService.DeleteByDocumentIdAsync(documentId, cancellationToken);
            }

            var embeddings = await _embeddingService.CreateEmbeddingsAsync(chunkTexts, cancellationToken);
            var vectorPoints = new List<VectorDocumentPoint>(embeddings.Count);

            for (var index = 0; index < embeddings.Count; index++)
            {
                vectorPoints.Add(new VectorDocumentPoint
                {
                    PointId = BuildPointId(documentId, index),
                    DocumentId = documentId,
                    ChunkIndex = index,
                    Category = document.Category,
                    Title = document.Name,
                    Content = chunkTexts[index],
                    BlobPath = document.Path,
                    IsActive = true,
                    Vector = embeddings[index]
                });
            }

            await _vectorStoreService.UpsertAsync(vectorPoints, cancellationToken);

            return await CompleteAsync(
                documentId,
                IngestionStatuses.Completed,
                chunkTexts.Count,
                vectorPoints.Count,
                null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document ingestion failed for document {DocumentId}.", documentId);

            return await CompleteAsync(
                documentId,
                IngestionStatuses.Failed,
                0,
                0,
                ex.Message,
                cancellationToken);
        }
    }

    private async Task<IngestionResultDto> CompleteAsync(
        long documentId,
        string status,
        int chunkCount,
        int vectorCount,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(
            documentId,
            status,
            status == IngestionStatuses.Completed ? DateTime.UtcNow : null,
            errorMessage,
            chunkCount > 0 ? chunkCount : null,
            cancellationToken);

        return new IngestionResultDto
        {
            DocumentId = documentId,
            Status = status,
            ChunkCount = chunkCount,
            VectorCount = vectorCount,
            CollectionName = _vectorStoreSettings.CollectionName,
            ErrorMessage = errorMessage
        };
    }

    private async Task UpdateStatusAsync(
        long documentId,
        string status,
        DateTime? ingestedAt,
        string? errorMessage,
        int? chunkCount,
        CancellationToken cancellationToken)
    {
        var response = await _documentRepo.UpdateIngestionAsync(
            documentId,
            status,
            ingestedAt,
            errorMessage,
            chunkCount,
            cancellationToken);

        EnsureSuccess(response);
    }

    private static DocumentMstrDto? MapDocument(MSSQLResponse? response)
    {
        EnsureSuccess(response);

        if (response?.Data is not DataSet { Tables.Count: > 0 } dataSet
            || dataSet.Tables[0].Rows.Count == 0)
        {
            return null;
        }

        var row = dataSet.Tables[0].Rows[0];
        return new DocumentMstrDto
        {
            DocumentId = Convert.ToInt64(row["dm_id"]),
            Category = Convert.ToString(row["dm_category"]) ?? string.Empty,
            Name = Convert.ToString(row["dm_name"]) ?? string.Empty,
            Path = Convert.ToString(row["dm_path"]) ?? string.Empty,
            CreatedBy = Convert.ToString(row["dm_created_by"]) ?? string.Empty,
            CreatedDate = row["dm_created_date"] is DateTime createdDate ? createdDate : Convert.ToDateTime(row["dm_created_date"]),
            Active = Convert.ToString(row["dm_active"]) ?? "N"
        };
    }

    private static void EnsureSuccess(MSSQLResponse? response)
    {
        var outputCode = int.TryParse(Convert.ToString(response?.OutputParameters?.FirstOrDefault(p =>
            string.Equals(p.ParameterName, "@outputCode", StringComparison.OrdinalIgnoreCase))?.Value), out var code)
            ? code
            : -1;

        var outputMsg = Convert.ToString(response?.OutputParameters?.FirstOrDefault(p =>
            string.Equals(p.ParameterName, "@outputMsg", StringComparison.OrdinalIgnoreCase))?.Value);

        if (outputCode != 1)
        {
            throw new ValidationException(string.IsNullOrWhiteSpace(outputMsg) ? "Document operation failed." : outputMsg);
        }
    }

    private static string BuildPointId(long documentId, int chunkIndex)
    {
        return $"{documentId:D10}-{chunkIndex:D6}";
    }

    private static string ExtractFileName(string blobPath)
    {
        var blobName = blobPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? blobPath;
        var underscoreIndex = blobName.IndexOf('_');
        return underscoreIndex >= 0 && underscoreIndex < blobName.Length - 1
            ? blobName[(underscoreIndex + 1)..]
            : blobName;
    }

    private static string ResolveMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".wmv" => "video/x-ms-wmv",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".m4v" => "video/x-m4v",
            ".mpeg" or ".mpg" => "video/mpeg",
            _ => "application/octet-stream"
        };
    }
}
