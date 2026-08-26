using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Domain.Interfaces;

public interface IJwtTokenService
{
    GeneratedAccessToken GenerateAccessToken(AuthenticatedAdminUser user, bool rememberMe);
}
