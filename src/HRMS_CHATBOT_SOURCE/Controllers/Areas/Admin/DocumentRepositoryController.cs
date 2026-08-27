using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;
using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_CHATBOT_SOURCE.Controllers.Areas.Admin;

[Area("Admin")]
[AdminAuthorize]
public class DocumentRepositoryController : Controller
{
    private readonly IDocumentLogic _documentLogic;

    public DocumentRepositoryController(IDocumentLogic documentLogic)
    {
        _documentLogic = documentLogic;
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
