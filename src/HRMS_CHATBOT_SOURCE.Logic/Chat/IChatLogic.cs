using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using Microsoft.AspNetCore.Http;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface IChatLogic
{
    Task<ChatTurnResponse> SendMessageAsync(ChatTurnRequest? request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamChunk> SendMessageStreamAsync(
        ChatTurnRequest? request,
        CancellationToken cancellationToken = default);

    Task<VoiceTranscriptionResponse> TranscribeVoiceAsync(
        string? mobile,
        IFormFile? audio,
        CancellationToken cancellationToken = default);

    Task<VoiceInputEnabledResponse> GetVoiceInputEnabledAsync(
        string? mobile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active mobile numbers (from user_profile) used to populate the chat UI's mobile-number dropdown.
    /// </summary>
    Task<ActiveMobileNumbersResponse> GetActiveMobileNumbersAsync(CancellationToken cancellationToken = default);
}
