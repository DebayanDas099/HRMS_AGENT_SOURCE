using System.Security.Claims;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace HRMS_CHATBOT_SOURCE.Tests.Security;

public class AdminAuthorizeAttributeTests
{
    [Fact]
    public void OnAuthorization_UnvalidatedJwtInCookie_DoesNotAuthorize()
    {
        var context = new DefaultHttpContext();
        context.Request.Cookies = new TestRequestCookieCollection(
            new Dictionary<string, string> { ["hrms_admin_token"] = "eyJhbGciOiJSUzI1NiJ9.payload.signature|@|cipher" });

        var filterContext = CreateAuthorizationContext(context);
        new AdminAuthorizeAttribute().OnAuthorization(filterContext);

        Assert.IsType<RedirectToActionResult>(filterContext.Result);
    }

    [Fact]
    public void OnAuthorization_ValidatedAdminPrincipal_AllowsRequest()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("UserId", "DG123"),
                new Claim(AdminClaimTypes.IsAdmin, "Y")
            ],
            authenticationType: "Bearer"))
        };

        var filterContext = CreateAuthorizationContext(context);
        new AdminAuthorizeAttribute().OnAuthorization(filterContext);

        Assert.Null(filterContext.Result);
    }

    private static AuthorizationFilterContext CreateAuthorizationContext(HttpContext httpContext)
    {
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        return new AuthorizationFilterContext(actionContext, []);
    }

    private sealed class TestRequestCookieCollection(Dictionary<string, string> cookies) : IRequestCookieCollection
    {
        public string? this[string key] => cookies.TryGetValue(key, out var value) ? value : null;

        public int Count => cookies.Count;

        public ICollection<string> Keys => cookies.Keys;

        public bool ContainsKey(string key) => cookies.ContainsKey(key);

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => cookies.GetEnumerator();

        public bool TryGetValue(string key, out string? value) => cookies.TryGetValue(key, out value);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
