using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using HRMS_CHATBOT_SOURCE.Logic.Adapter;
using HRMS_CHATBOT_SOURCE.Repo.Admin;
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
    private readonly IUserProfileRepo _userProfileRepo;
    private readonly IServiceContext _serviceContext;
    private readonly ILogger<ChatLogic> _logger;

    public ChatLogic(
        IAgentAccessService agentAccessService,
        IHrmsChatRuntime chatRuntime,
        IUserProfileRepo userProfileRepo,
        IServiceContext serviceContext,
        ILogger<ChatLogic> logger)
    {
        _agentAccessService = agentAccessService;
        _chatRuntime = chatRuntime;
        _userProfileRepo = userProfileRepo;
        _serviceContext = serviceContext;
        _logger = logger;
    }

    public async Task<ChatTurnResponse> SendMessageAsync(
        ChatTurnRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ValidationException("Invalid request.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ValidationException("Message is required.");
        }

        var mobile = !string.IsNullOrWhiteSpace(request.Mobile)
            ? request.Mobile.Trim()
            : _serviceContext.CurrentUser?.Mobile?.Trim();

        if (string.IsNullOrWhiteSpace(mobile))
        {
            _logger.LogWarning(
                "Chat mobile missing: request.Mobile={RequestMobile}, authenticatedMobile={AuthenticatedMobile}, userAuthenticated={UserAuthenticated}",
                request.Mobile,
                _serviceContext.CurrentUser?.Mobile,
                _serviceContext.RequestContext?.User?.Identity?.IsAuthenticated);

            throw new ValidationException("Mobile number is required.");
        }
        var enabledAgents = await _agentAccessService
            .GetEnabledAgentNamesAsync(mobile, cancellationToken)
            .ConfigureAwait(false);

        if (enabledAgents.Count == 0)
        {
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
            .RunAsync(enabledAgents, request.ConversationId, request.Message, mobile, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ActiveMobileNumbersResponse> GetActiveMobileNumbersAsync(
        CancellationToken cancellationToken = default)
    {
        var dbResponse = await _userProfileRepo
            .GetActiveMobileNumbersAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ActiveMobileNumbersResponse
        {
            MobileNumbers = UserProfileAdapter.MapActiveMobileNumbers(dbResponse)
        };
    }
}
