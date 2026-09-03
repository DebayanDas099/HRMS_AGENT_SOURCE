using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using HRMS_CHATBOT_SOURCE.Logic.Adapter;
using HRMS_CHATBOT_SOURCE.Repo.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Logic;

public class ChatLogic : IChatLogic
{
    private const string AccessDeniedReply =
        "Your mobile number is not registered for this service. Please contact HR/IT support.";

    private readonly IAgentAccessService _agentAccessService;
    private readonly IHrmsChatRuntime _chatRuntime;
    private readonly IUserProfileRepo _userProfileRepo;
    private readonly ISpeechTranslationService _speechTranslationService;
    private readonly IServiceContext _serviceContext;
    private readonly AzureSpeechSettings _speechSettings;
    private readonly ILogger<ChatLogic> _logger;

    public ChatLogic(
        IAgentAccessService agentAccessService,
        IHrmsChatRuntime chatRuntime,
        IUserProfileRepo userProfileRepo,
        ISpeechTranslationService speechTranslationService,
        IServiceContext serviceContext,
        IOptions<AzureSpeechSettings> speechSettings,
        ILogger<ChatLogic> logger)
    {
        _agentAccessService = agentAccessService;
        _chatRuntime = chatRuntime;
        _userProfileRepo = userProfileRepo;
        _speechTranslationService = speechTranslationService;
        _serviceContext = serviceContext;
        _speechSettings = speechSettings.Value;
        _logger = logger;
    }

    public Task<ChatTurnResponse> SendMessageAsync(
        ChatTurnRequest? request,
        CancellationToken cancellationToken = default) =>
        CollectStreamResponseAsync(request, cancellationToken);

    public async IAsyncEnumerable<ChatStreamChunk> SendMessageStreamAsync(
        ChatTurnRequest? request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ValidationException("Invalid request.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ValidationException("Message is required.");
        }

        var mobile = ResolveMobile(request.Mobile);
        if (string.IsNullOrWhiteSpace(mobile))
        {
            _logger.LogWarning(
                "Chat mobile missing: request.Mobile={RequestMobile}, authenticatedMobile={AuthenticatedMobile}, userAuthenticated={UserAuthenticated}",
                request.Mobile,
                _serviceContext.CurrentUser?.Mobile,
                _serviceContext.RequestContext?.User?.Identity?.IsAuthenticated);

            throw new ValidationException("Mobile number is required.");
        }

        var enabledAgents = await ResolveWorkflowAgentsAsync(mobile, cancellationToken).ConfigureAwait(false);
        if (enabledAgents.Count == 0)
        {
            _logger.LogWarning("Chat access denied: no agents enabled for mobile {Mobile}.", mobile);

            var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
                ? Guid.NewGuid().ToString("N")
                : request.ConversationId.Trim();

            yield return ChatStreamChunk.Done(conversationId, AccessDeniedReply, [], null);
            yield break;
        }

        await foreach (var chunk in _chatRuntime
            .RunStreamAsync(enabledAgents, request.ConversationId, request.Message, mobile, cancellationToken)
            .ConfigureAwait(false))
        {
            yield return chunk;
        }
    }

    public async Task<VoiceTranscriptionResponse> TranscribeVoiceAsync(
        string? mobile,
        IFormFile? audio,
        CancellationToken cancellationToken = default)
    {
        var resolvedMobile = ResolveMobile(mobile);
        if (string.IsNullOrWhiteSpace(resolvedMobile))
        {
            throw new ValidationException("Mobile number is required.");
        }

        if (!await _agentAccessService.IsVoiceInputEnabledAsync(resolvedMobile, cancellationToken).ConfigureAwait(false))
        {
            throw new ValidationException(VoiceInputEnabledResponse.DefaultDisabledMessage);
        }

        if (audio == null || audio.Length == 0)
        {
            throw new ValidationException("Audio file is required.");
        }

        if (audio.Length > _speechSettings.MaxAudioBytes)
        {
            throw new ValidationException($"Audio file exceeds the maximum size of {_speechSettings.MaxAudioBytes} bytes.");
        }

        await using var stream = audio.OpenReadStream();
        return await _speechTranslationService
            .TranscribeAndTranslateToEnglishAsync(stream, audio.ContentType ?? "audio/wav", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VoiceInputEnabledResponse> GetVoiceInputEnabledAsync(
        string? mobile,
        CancellationToken cancellationToken = default)
    {
        var resolvedMobile = ResolveMobile(mobile);
        if (string.IsNullOrWhiteSpace(resolvedMobile))
        {
            return new VoiceInputEnabledResponse
            {
                VoiceEnabled = false,
                DisabledMessage = VoiceInputEnabledResponse.DefaultDisabledMessage
            };
        }

        var enabled = await _agentAccessService
            .IsVoiceInputEnabledAsync(resolvedMobile, cancellationToken)
            .ConfigureAwait(false);

        return new VoiceInputEnabledResponse
        {
            VoiceEnabled = enabled,
            DisabledMessage = VoiceInputEnabledResponse.DefaultDisabledMessage
        };
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

    private async Task<ChatTurnResponse> CollectStreamResponseAsync(
        ChatTurnRequest? request,
        CancellationToken cancellationToken)
    {
        ChatTurnResponse? response = null;

        await foreach (var chunk in SendMessageStreamAsync(request, cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(chunk.Type, "done", StringComparison.OrdinalIgnoreCase))
            {
                response = new ChatTurnResponse
                {
                    ConversationId = chunk.ConversationId ?? string.Empty,
                    Reply = chunk.Reply ?? string.Empty,
                    EnabledAgents = chunk.EnabledAgents ?? [],
                    LastSpeaker = chunk.LastSpeaker
                };
            }
            else if (string.Equals(chunk.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException(chunk.Message ?? "Chat request failed.");
            }
        }

        return response ?? throw new InvalidOperationException("Chat stream ended without a done chunk.");
    }

    private string? ResolveMobile(string? requestMobile) =>
        !string.IsNullOrWhiteSpace(requestMobile)
            ? requestMobile.Trim()
            : _serviceContext.CurrentUser?.Mobile?.Trim();

    private async Task<List<string>> ResolveWorkflowAgentsAsync(
        string mobile,
        CancellationToken cancellationToken)
    {
        var enabledAgents = (await _agentAccessService
            .GetWorkflowAgentNamesAsync(mobile, cancellationToken)
            .ConfigureAwait(false)).ToList();

        // LeaveApprovalAgent is never granted by GetWorkflowAgentNamesAsync (group-based,
        // driven by the client-supplied mobile) - it is added here, and only here, when
        // both of these hold:
        //   1. _serviceContext.CurrentUser says IsAdmin=Y - JwtValidationMiddleware
        //      populates this by cryptographically validating the request's token.
        //   2. That token was presented via an explicit header (HasExplicitHeaderToken),
        //      not merely the ambient hrms_admin_token cookie.
        // (2) matters because the admin cookie is Path=/ and browser-attached to every
        // same-origin request automatically - including a fetch() from the public,
        // anonymous /chat page, if the same browser also happens to be logged into
        // /Admin in another tab. Without this check, that employee-facing page would
        // silently gain an admin-only capability it never asked for. The Conversations
        // test-chat panel deliberately attaches its admin token via header
        // (admin-auth.js's getAuthHeaders()), so it is unaffected by this extra check.
        var isVerifiedAdmin = string.Equals(
            _serviceContext.CurrentUser?.IsAdmin,
            "Y",
            StringComparison.OrdinalIgnoreCase)
            && _serviceContext.RequestContext is not null
            && JwtTokenValidator.HasExplicitHeaderToken(_serviceContext.RequestContext);

        if (isVerifiedAdmin && !enabledAgents.Contains(AgentNames.LeaveApproval, StringComparer.OrdinalIgnoreCase))
        {
            enabledAgents.Add(AgentNames.LeaveApproval);
        }

        return enabledAgents;
    }
}
