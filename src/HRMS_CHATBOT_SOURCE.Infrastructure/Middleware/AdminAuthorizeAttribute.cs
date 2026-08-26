using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!AdminAuthHelper.IsAuthenticatedAdmin(context.HttpContext))
        {
            context.Result = new RedirectToActionResult("Login", "Account", new { area = "Admin" });
        }
    }
}
