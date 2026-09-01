using System.Security.Claims;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using Microsoft.AspNetCore.Http;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Security;

public static class AdminAuthHelper
{
    public static bool IsAuthenticatedAdmin(HttpContext? context)
    {
        var user = context?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return string.Equals(
            user.FindFirst(AdminClaimTypes.IsAdmin)?.Value,
            "Y",
            StringComparison.OrdinalIgnoreCase);
    }

    public static string? FindClaim(ClaimsPrincipal user, HttpContext? context, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = user.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
