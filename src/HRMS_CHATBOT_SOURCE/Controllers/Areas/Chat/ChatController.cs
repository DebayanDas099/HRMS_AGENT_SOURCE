using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_CHATBOT_SOURCE.Controllers.Areas.Chat;

[Area("Chat")]
[ApiController]
[AllowAnonymous]
[Consumes("application/json")]
[Produces("application/json")]
public class ChatController : Controller
{
    private readonly IChatLogic _chatLogic;

    public ChatController(IChatLogic chatLogic)
    {
        _chatLogic = chatLogic;
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
    /// Sends one chat turn through the Supervisor handoff workflow for the user identified by mobile number.
    /// </summary>
    [HttpPost]
    [Route("api/ChatStreamAsync")]
    [ProducesResponseType(typeof(ChatTurnResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ChatTurnResponse> ChatStreamAsync(
        [FromBody] ChatTurnRequest request,
        CancellationToken cancellationToken)
    {
        return await _chatLogic.SendMessageAsync(request, cancellationToken);
    }

    /// <summary>
    /// Lists active mobile numbers, used to populate the chat UI's mobile-number dropdown.
    /// </summary>
    [HttpGet]
    [Route("api/GetActiveMobileNumbersAsync")]
    [ProducesResponseType(typeof(ActiveMobileNumbersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActiveMobileNumbersResponse> GetActiveMobileNumbersAsync(CancellationToken cancellationToken)
    {
        return await _chatLogic.GetActiveMobileNumbersAsync(cancellationToken);
    }
}
