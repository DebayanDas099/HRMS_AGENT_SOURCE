using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface IChatLogic
{
    Task<ChatTurnResponse> SendMessageAsync(ChatTurnRequest? request, CancellationToken cancellationToken = default);
}
