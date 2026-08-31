using System.ComponentModel.DataAnnotations;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;

namespace HRMS_CHATBOT_SOURCE.Logic;

public class ChatLogic : IChatLogic
{
    private readonly IAgentAccessService _agentAccessService;
    private readonly IHrmsChatRuntime _chatRuntime;

    public ChatLogic(IAgentAccessService agentAccessService, IHrmsChatRuntime chatRuntime)
    {
        _agentAccessService = agentAccessService;
        _chatRuntime = chatRuntime;
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

        var enabledAgents = await _agentAccessService
            .GetEnabledAgentNamesAsync(request.Mobile.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return await _chatRuntime
            .RunAsync(enabledAgents, request.ConversationId, request.Message, cancellationToken)
            .ConfigureAwait(false);
    }
}
