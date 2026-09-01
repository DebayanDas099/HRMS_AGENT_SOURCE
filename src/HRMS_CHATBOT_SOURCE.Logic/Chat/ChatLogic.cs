using System.ComponentModel.DataAnnotations;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Logic;

public class ChatLogic : IChatLogic
{
    /// <summary>
    /// The exact, fixed refusal an unregistered or fully-disabled number gets - and
    /// nothing else. No hint about what the service does, who is registered, or what
    /// capabilities exist. This decision is made here, deterministically, before any
    /// workflow is built: the model is never the one who decides who someone is.
    /// </summary>
    private const string AccessDeniedReply =
        "Your mobile number is not registered for this service. Please contact HR/IT support.";

    private readonly IAgentAccessService _agentAccessService;
    private readonly IHrmsChatRuntime _chatRuntime;
    private readonly ILogger<ChatLogic> _logger;

    public ChatLogic(IAgentAccessService agentAccessService, IHrmsChatRuntime chatRuntime, ILogger<ChatLogic> logger)
    {
        _agentAccessService = agentAccessService;
        _chatRuntime = chatRuntime;
        _logger = logger;
    }

    public async Task<ChatTurnResponse> SendMessageAsync(
        ChatTurnRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Mobile))
        {
            throw new ValidationException("Mobile number is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ValidationException("Message is required.");
        }

        var mobile = request.Mobile.Trim();
        var enabledAgents = await _agentAccessService
            .GetEnabledAgentNamesAsync(mobile, cancellationToken)
            .ConfigureAwait(false);

        if (enabledAgents.Count == 0)
        {
            // Short-circuit here, not inside the workflow: HandoffWorkflowTemplate
            // always force-adds Supervisor to whatever agent set it is given, so an
            // empty list reaching it would still produce a working conversation.
            // The only way to guarantee "a single controlled response and nothing
            // else" is to never build the workflow at all for a denied number.
            _logger.LogWarning("Chat access denied: no agents enabled for mobile {Mobile}.", mobile);

            return new ChatTurnResponse
            {
                ConversationId = string.IsNullOrWhiteSpace(request.ConversationId)
                    ? Guid.NewGuid().ToString("N")
                    : request.ConversationId.Trim(),
                Reply = AccessDeniedReply,
                EnabledAgents = [],
                LastSpeaker = null
            };
        }

        return await _chatRuntime
            .RunAsync(enabledAgents, request.ConversationId, request.Message, cancellationToken)
            .ConfigureAwait(false);
    }
}
