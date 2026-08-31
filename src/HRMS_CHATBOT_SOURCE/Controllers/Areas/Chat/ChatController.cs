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
public class ChatController : ControllerBase
{
    private readonly IChatLogic _chatLogic;

    public ChatController(IChatLogic chatLogic)
    {
        _chatLogic = chatLogic;
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
}
