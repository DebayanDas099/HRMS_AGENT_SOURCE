using HRMS_CHATBOT_SOURCE.Domain.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "Admin" });
            return;
        }

        if (!string.Equals(
                user.FindFirst(AdminClaimTypes.IsAdmin)?.Value,
                "Y",
                StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "Admin" });
        }
    }
}
