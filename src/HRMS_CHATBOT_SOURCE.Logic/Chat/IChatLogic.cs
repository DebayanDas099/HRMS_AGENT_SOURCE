using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface IChatLogic
{
    Task<ChatTurnResponse> SendMessageAsync(ChatTurnRequest? request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists active mobile numbers (from user_profile) used to populate the chat UI's mobile-number dropdown.
    /// </summary>
    Task<ActiveMobileNumbersResponse> GetActiveMobileNumbersAsync(CancellationToken cancellationToken = default);
}
