using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;
using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using HRMS_CHATBOT_SOURCE.Logic;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using HRMS_CHATBOT_SOURCE.RAG.Services;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_CHATBOT_SOURCE.Controllers.Areas.Admin;

[Area("Admin")]
[AdminAuthorize]
public class DocumentRepositoryController : Controller
{
    private readonly IDocumentLogic _documentLogic;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly LuceneIndexRebuilder _indexRebuilder;

    public DocumentRepositoryController(
        IDocumentLogic documentLogic,
        IVectorSearchService vectorSearchService,
        LuceneIndexRebuilder indexRebuilder)
    {
        _documentLogic = documentLogic;
        _vectorSearchService = vectorSearchService;
        _indexRebuilder = indexRebuilder;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<DocumentStatisticsDto?> GetStatistics(CancellationToken cancellationToken)
    {
        return await _documentLogic.GetStatisticsAsync(cancellationToken);
    }

    [HttpGet]
    [Produces("application/json")]
    public async Task<DocumentListResponseDto?> GetList(
        string? category,
        string? search_text,
        int page_number = 1,
        int page_size = 10,
        CancellationToken cancellationToken = default)
    {
        return await _documentLogic.GetDocumentsAsync(category, search_text, page_number, page_size, cancellationToken);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<BulkDocumentUploadResponse?> BulkUpload(
        [FromForm] string category,
        [FromForm] string title,
        [FromForm] List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var createdBy = AdminAuthHelper.FindClaim(User, HttpContext, "UserId", "UserName")
            ?? User.Identity?.Name
            ?? "SYSTEM";

        return await _documentLogic.BulkUploadAsync(category, files, title, createdBy, cancellationToken);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<DocumentMstrDto?> UpdateActive(
        [FromBody] UpdateDocumentActiveRequest request,
        CancellationToken cancellationToken)
    {
        return await _documentLogic.UpdateActiveAsync(request.DocumentId, request.IsActive, cancellationToken);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IngestionResultDto?> IngestDocument(
        [FromBody] IngestDocumentRequest request,
        CancellationToken cancellationToken)
    {
        return await _documentLogic.IngestDocumentAsync(request.DocumentId, cancellationToken);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Consumes("application/json")]
    [Produces("application/json")]
    public async Task<DeleteDocumentResponse?> Delete(
        [FromBody] DeleteDocumentRequest request,
        CancellationToken cancellationToken)
    {
        return await _documentLogic.DeleteDocumentAsync(request.DocumentId, cancellationToken);
    }

    /// <summary>
    /// Exercises hybrid retrieval directly. Set include_lexical=false to compare
    /// hybrid against dense-only, which is how the exact-term recall gain is verified.
    /// </summary>
    [HttpGet]
    [Produces("application/json")]
    public async Task<RetrievalResultDto> Search(
        string q,
        string? category = null,
        int top_k = RetrievalDefaults.TopK,
        bool include_lexical = true,
        bool rerank = true,
        bool active_only = true,
        CancellationToken cancellationToken = default)
    {
        var response = await _vectorSearchService.SearchAsync(
            new RetrievalQuery
            {
                Query = q,
                TopK = top_k,
                Category = category,
                ActiveOnly = active_only,
                EnableLexical = include_lexical,
                EnableRerank = rerank
            },
            cancellationToken);

        return new RetrievalResultDto
        {
            Query = response.Query,
            Confidence = Math.Round(response.Confidence, 4),
            IsConfident = response.IsConfident,
            UsedRerank = response.UsedRerank,
            UsedLexical = response.UsedLexical,
            DenseCount = response.DenseCount,
            SparseCount = response.SparseCount,
            FusedCount = response.FusedCount,
            ElapsedMilliseconds = response.ElapsedMilliseconds,
            Results = response.Results.Select(result => new RetrievalHitDto
            {
                DocumentId = result.DocumentId,
                ChunkIndex = result.ChunkIndex,
                Category = result.Category,
                Title = result.Title,
                SectionPath = result.SectionPath,
                Content = result.Content,
                Score = result.Score,
                MatchedBy = result.MatchedBy
            }).ToList()
        };
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Produces("application/json")]
    public async Task<IActionResult> RebuildSearchIndex(CancellationToken cancellationToken)
    {
        var indexed = await _indexRebuilder.RebuildAsync(cancellationToken);
        return Ok(new { chunk_count = indexed });
    }

    [HttpGet]
    public async Task<IActionResult> Download(long document_id, CancellationToken cancellationToken)
    {
        var result = await _documentLogic.DownloadDocumentAsync(document_id, cancellationToken);
        if (result == null || result.FileContent.Length == 0)
        {
            return NotFound();
        }

        return File(result.FileContent, result.ContentType, result.FileName);
    }
}
