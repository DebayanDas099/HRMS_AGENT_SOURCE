using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface IAdminLogic
{
    Task<LoginResponse?> ValidateLogin(LoginRequest? request, CancellationToken cancellationToken = default);
    LogoutResponse Logout();
}
