using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Json;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Controllers.Areas.Chat;

[Area("Chat")]
[ApiController]
[AllowAnonymous]
public class ChatController : Controller
{
    private readonly IChatLogic _chatLogic;
    private readonly IDocumentLogic _documentLogic;

    public ChatController(
        IChatLogic chatLogic,
        IDocumentLogic documentLogic)
    {
        _chatLogic = chatLogic;
        _documentLogic = documentLogic;
    }

    /// <summary>
    /// Serves the anonymous, no-login WhatsApp-style chat UI.
    /// </summary>
    [HttpGet]
    [Route("Chat")]
    [Route("Chat/Index")]
    [Produces("text/html")]
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Sends one chat turn through the Supervisor handoff workflow, streaming NDJSON chunks.
    /// </summary>
    [HttpPost]
    [Route("api/ChatStreamAsync")]
    [Consumes("application/json")]
    [Produces("application/x-ndjson")]
    public async Task ChatStreamAsync(
        [FromBody] ChatTurnRequest request,
        CancellationToken cancellationToken)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        _documentLogic.SetChatTurnContext(request?.Mobile, baseUrl);

        Response.ContentType = "application/x-ndjson";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            await foreach (var chunk in _chatLogic.SendMessageStreamAsync(request, cancellationToken))
            {
                var line = JsonConvert.SerializeObject(chunk, DomainJsonSerializerSettings.Default) + "\n";
                await Response.WriteAsync(line, cancellationToken).ConfigureAwait(false);
                await Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _documentLogic.ClearChatTurnContext();
        }
    }

    /// <summary>
    /// Transcribes and translates voice input (bn/hi/en) to English for use as a chat prompt.
    /// </summary>
    [HttpPost]
    [Route("api/TranscribeVoiceAsync")]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(VoiceTranscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<VoiceTranscriptionResponse> TranscribeVoiceAsync(
        [FromForm(Name = "mobile")] string? mobile,
        [FromForm(Name = "audio")] IFormFile audio,
        CancellationToken cancellationToken)
    {
        return await _chatLogic.TranscribeVoiceAsync(mobile, audio, cancellationToken);
    }

    /// <summary>
    /// Returns whether voice input is enabled for the mobile number's user group.
    /// </summary>
    [HttpGet]
    [Route("api/GetVoiceInputEnabledAsync")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(VoiceInputEnabledResponse), StatusCodes.Status200OK)]
    public async Task<VoiceInputEnabledResponse> GetVoiceInputEnabledAsync(
        string? mobile,
        CancellationToken cancellationToken)
    {
        return await _chatLogic.GetVoiceInputEnabledAsync(mobile, cancellationToken);
    }

    [HttpGet]
    [Route("api/ChatDocumentDownload")]
    public async Task<IActionResult> ChatDocumentDownload(
        string? token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest("Missing token.");
        }

        if (!_documentLogic.TryDecodeDocumentDownloadToken(token, out var documentId))
        {
            return BadRequest("Invalid token.");
        }

        var result = await _documentLogic.DownloadDocumentAsync(documentId, cancellationToken);
        if (result == null || result.FileContent.Length == 0)
        {
            return NotFound();
        }

        return File(result.FileContent, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Lists active mobile numbers, used to populate the chat UI's mobile-number dropdown.
    /// </summary>
    [HttpGet]
    [Route("api/GetActiveMobileNumbersAsync")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ActiveMobileNumbersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActiveMobileNumbersResponse> GetActiveMobileNumbersAsync(CancellationToken cancellationToken)
    {
        return await _chatLogic.GetActiveMobileNumbersAsync(cancellationToken);
    }
}
