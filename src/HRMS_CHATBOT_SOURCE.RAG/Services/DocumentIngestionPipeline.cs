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
    private readonly ILexicalIndex _lexicalIndex;
    private readonly IngestionSettings _ingestionSettings;
    private readonly VectorStoreSettings _vectorStoreSettings;
    private readonly ILogger<DocumentIngestionPipeline> _logger;

    public DocumentIngestionPipeline(
        IDocumentRepo documentRepo,
        IDocumentBlobService documentBlobService,
        IDocumentChunkerService documentChunkerService,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ILexicalIndex lexicalIndex,
        IOptions<IngestionSettings> ingestionOptions,
        IOptions<VectorStoreSettings> vectorStoreOptions,
        ILogger<DocumentIngestionPipeline> logger)
    {
        _documentRepo = documentRepo;
        _documentBlobService = documentBlobService;
        _documentChunkerService = documentChunkerService;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _lexicalIndex = lexicalIndex;
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

            // Chunk metadata is carried through rather than projected away: the section
            // breadcrumb is what turns a citation from a bare title into a locatable one.
            var preparedChunks = chunks
                .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Content))
                .Select(chunk => new PreparedChunk(
                    chunk.Content.Trim(),
                    chunk.SectionPath ?? string.Empty,
                    chunk.StartOffset,
                    chunk.EndOffset))
                .ToList();

            if (preparedChunks.Count == 0)
            {
                return await CompleteAsync(
                    documentId,
                    IngestionStatuses.Failed,
                    0,
                    0,
                    "Document chunks did not contain searchable text.",
                    cancellationToken);
            }

            var chunkTexts = preparedChunks.Select(chunk => chunk.Content).ToList();

            bool isDocPresent = await _vectorStoreService.ExistsByDocumentIdAsync(documentId, cancellationToken);
            if (isDocPresent)
            {
                await _vectorStoreService.DeleteByDocumentIdAsync(documentId, cancellationToken);
            }

            var embeddings = await _embeddingService.CreateEmbeddingsAsync(chunkTexts, cancellationToken);
            var vectorPoints = new List<VectorDocumentPoint>(embeddings.Count);

            var lexicalDocuments = new List<LexicalDocument>(embeddings.Count);
            var isActive = string.Equals(document.Active, "Y", StringComparison.OrdinalIgnoreCase);

            for (var index = 0; index < embeddings.Count; index++)
            {
                var chunk = preparedChunks[index];

                vectorPoints.Add(new VectorDocumentPoint
                {
                    DocumentId = documentId,
                    ChunkIndex = index,
                    Category = document.Category,
                    Title = document.Name,
                    Content = chunk.Content,
                    BlobPath = document.Path,
                    SectionPath = chunk.SectionPath,
                    StartOffset = chunk.StartOffset,
                    EndOffset = chunk.EndOffset,
                    IsActive = isActive,
                    Vector = embeddings[index]
                });

                lexicalDocuments.Add(new LexicalDocument
                {
                    DocumentId = documentId,
                    ChunkIndex = index,
                    Category = document.Category,
                    Title = document.Name,
                    Content = chunk.Content,
                    SectionPath = chunk.SectionPath,
                    StartOffset = chunk.StartOffset,
                    EndOffset = chunk.EndOffset,
                    IsActive = isActive
                });
            }

            await _vectorStoreService.UpsertAsync(vectorPoints, cancellationToken);

            var lexicalError = await IndexLexicalAsync(documentId, lexicalDocuments, cancellationToken);

            return await CompleteAsync(
                documentId,
                IngestionStatuses.Completed,
                chunkTexts.Count,
                vectorPoints.Count,
                lexicalError,
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

    public async Task<bool> RemoveFromIndexAsync(long documentId, CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            return false;
        }

        var isDocPresent = await _vectorStoreService.ExistsByDocumentIdAsync(documentId, cancellationToken);

        if (isDocPresent)
        {
            await _vectorStoreService.DeleteByDocumentIdAsync(documentId, cancellationToken);
            _logger.LogInformation("Removed vector index entries for document {DocumentId}.", documentId);
        }

        // Always attempted, even when the vector side was already clear, so a partially
        // ingested document cannot leave chunks searchable through the lexical index.
        try
        {
            await _lexicalIndex.DeleteDocumentAsync(documentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove document {DocumentId} from the lexical index.", documentId);
        }

        return isDocPresent;
    }

    public async Task SetDocumentActiveAsync(
        long documentId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (documentId <= 0)
        {
            return;
        }

        await _vectorStoreService.SetActiveAsync(documentId, isActive, cancellationToken);
        await _lexicalIndex.SetActiveAsync(documentId, isActive, cancellationToken);
    }

    /// <summary>
    /// Writes the chunks to the lexical index, returning a message when it fails.
    /// <para>
    /// The two stores are not written atomically. If Qdrant succeeded and Lucene did not,
    /// the document genuinely is searchable — just not lexically — so reporting the
    /// ingestion as Failed would misrepresent its state. It is recorded as a warning
    /// instead, and <see cref="LuceneIndexRebuilder"/> repairs the divergence.
    /// </para>
    /// </summary>
    private async Task<string?> IndexLexicalAsync(
        long documentId,
        IReadOnlyList<LexicalDocument> documents,
        CancellationToken cancellationToken)
    {
        try
        {
            await _lexicalIndex.ReplaceDocumentAsync(documentId, documents, cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lexical indexing failed for document {DocumentId}.", documentId);
            return $"Indexed for vector search, but keyword indexing failed: {ex.Message}";
        }
    }

    private sealed record PreparedChunk(string Content, string SectionPath, int StartOffset, int EndOffset);

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
