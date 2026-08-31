using System.ComponentModel.DataAnnotations;
using HRMS_CHATBOT_SOURCE.Agent;
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
    private readonly IAgentAccessService _agentAccessService;
    private readonly IHrmsChatRuntime _chatRuntime;

    public ChatController(IAgentAccessService agentAccessService, IHrmsChatRuntime chatRuntime)
    {
        _agentAccessService = agentAccessService;
        _chatRuntime = chatRuntime;
    }

    /// <summary>
    /// Sends one chat turn through the Supervisor handoff workflow for the user identified by mobile number.
    /// </summary>
    [HttpPost]
    [Route("api/ChatStreamAsync")]
    [ProducesResponseType(typeof(ChatTurnResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatTurnResponse>> ChatStreamAsync(
        [FromBody] ChatTurnRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Mobile))
        {
            throw new ValidationException("Mobile number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ValidationException("Message is required.");
        }

        var enabledAgents = await _agentAccessService
            .GetEnabledAgentNamesAsync(request.Mobile.Trim(), cancellationToken)
            .ConfigureAwait(false);

        var result = await _chatRuntime
            .RunAsync(enabledAgents, request.ConversationId, request.Message, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new ChatTurnResponse
        {
            ConversationId = result.ConversationId,
            Reply = result.Reply,
            EnabledAgents = result.EnabledAgents,
            LastSpeaker = result.LastSpeaker
        });
    }
}
