using System.Security.Claims;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Security;

public static class AdminClaimFactory
{
    public static IEnumerable<Claim> CreateClaims(AuthenticatedAdminUser user)
    {
        yield return new Claim("UserId", user.UserId);
        yield return new Claim("UserGroup", user.GroupCode);
        yield return new Claim("UserName", user.FullName);
        yield return new Claim("Device_Uuid", string.Empty);
        yield return new Claim("Device_Name", string.Empty);
        yield return new Claim("Device_Model", string.Empty);
        yield return new Claim("Device_Platform", string.Empty);
        yield return new Claim("Device_OS", string.Empty);
        yield return new Claim("Location", string.Empty);
        yield return new Claim("IP", string.Empty);
        yield return new Claim("MAC", string.Empty);
        yield return new Claim("UserAgent", string.Empty);
        yield return new Claim("Host", string.Empty);
        yield return new Claim(AdminClaimTypes.IsAdmin, user.IsAdmin ? "Y" : "N");
        yield return new Claim(AdminClaimTypes.GroupCode, user.GroupCode);
        yield return new Claim(AdminClaimTypes.Department, user.Department);

        if (!string.IsNullOrWhiteSpace(user.Designation))
        {
            yield return new Claim(AdminClaimTypes.Designation, user.Designation);
        }

        if (!string.IsNullOrWhiteSpace(user.EmployeeId))
        {
            yield return new Claim(AdminClaimTypes.EmployeeId, user.EmployeeId);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            yield return new Claim(AdminClaimTypes.Email, user.Email);
        }
    }
}
