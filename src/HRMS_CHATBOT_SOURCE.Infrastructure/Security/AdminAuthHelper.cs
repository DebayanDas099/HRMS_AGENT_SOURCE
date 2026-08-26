using System.IdentityModel.Tokens.Jwt;
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

        if (string.Equals(
                user.FindFirst(AdminClaimTypes.IsAdmin)?.Value,
                "Y",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(FindClaim(user, context, "UserId")))
        {
            return true;
        }

        var jwtUserId = FindJwtClaim(context, "UserId");
        return !string.IsNullOrWhiteSpace(jwtUserId);
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

        return FindJwtClaim(context, claimTypes);
    }

    public static string? FindJwtClaim(HttpContext? context, params string[] claimTypes)
    {
        var token = ReadToken(context);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                return null;
            }

            var jwt = handler.ReadJwtToken(token);
            foreach (var claimType in claimTypes)
            {
                var value = jwt.Claims.FirstOrDefault(claim =>
                    string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase))?.Value;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string? ReadToken(HttpContext? context)
    {
        if (context == null)
        {
            return null;
        }

        if (context.Request.Headers.TryGetValue("hrms_admin_token", out var headerToken)
            && !string.IsNullOrWhiteSpace(headerToken))
        {
            return NormalizeToken(headerToken.ToString());
        }

        if (context.Request.Cookies.TryGetValue("hrms_admin_token", out var cookieToken)
            && !string.IsNullOrWhiteSpace(cookieToken))
        {
            return NormalizeToken(cookieToken);
        }

        return null;
    }

    private static string? NormalizeToken(string token)
    {
        token = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? token[7..].Trim()
            : token.Trim();

        var suffixIndex = token.IndexOf("|@|", StringComparison.Ordinal);
        return suffixIndex > 0 ? token[..suffixIndex] : token;
    }
}
