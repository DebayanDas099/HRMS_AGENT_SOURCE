using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Repo.Admin;

public interface IUserProfileRepo
{
    Task<MSSQLResponse?> ValidateAdminLoginAsync(LoginRequest? request, CancellationToken cancellationToken = default);
    Task<string?> GetUserMobileByUserIdAsync(string? userId, CancellationToken cancellationToken = default);
    Task<string?> GetUserEmailByMobileAsync(string? mobile, CancellationToken cancellationToken = default);
    Task<MSSQLResponse?> UpdateLastAccessedAsync(string? userId, CancellationToken cancellationToken = default);
}
