using HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;
using HRMS_CHATBOT_SOURCE.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Tests.Security;

public class JwtValidationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AllowAnonymousWithoutToken_PassesThrough()
    {
        var context = CreateContext("/api/ChatStreamAsync", allowAnonymous: true);
        var (middleware, wasNextCalled) = CreateMiddleware();

        await middleware.InvokeAsync(context, CreateValidator());

        Assert.True(wasNextCalled());
        Assert.False(context.User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task InvokeAsync_AllowAnonymousWithValidToken_SetsUser()
    {
        var token = JwtTokenValidatorTests.GenerateToken();
        var context = CreateContext("/api/ChatStreamAsync", allowAnonymous: true);
        context.Request.Headers["hrms_admin_token"] = token.AccessToken;
        var (middleware, wasNextCalled) = CreateMiddleware();

        await middleware.InvokeAsync(context, CreateValidator());

        Assert.True(wasNextCalled());
        Assert.True(context.User.Identity?.IsAuthenticated);
        Assert.Equal("DG123", context.User.FindFirst("UserId")?.Value);
    }

    [Fact]
    public async Task InvokeAsync_ProtectedRouteWithoutToken_Returns401()
    {
        var context = CreateContext("/api/admin/reports", allowAnonymous: false);
        var (middleware, wasNextCalled) = CreateMiddleware();

        await middleware.InvokeAsync(context, CreateValidator());

        Assert.False(wasNextCalled());
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ProtectedRouteWithInvalidToken_Returns401()
    {
        var context = CreateContext("/api/admin/reports", allowAnonymous: false);
        context.Request.Headers["hrms_admin_token"] = "not-a-valid-token";
        var (middleware, wasNextCalled) = CreateMiddleware();

        await middleware.InvokeAsync(context, CreateValidator());

        Assert.False(wasNextCalled());
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    private static JwtTokenValidator CreateValidator() =>
        new(Options.Create(JwtTokenValidatorTests.Settings));

    private static DefaultHttpContext CreateContext(string path, bool allowAnonymous)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var metadata = allowAnonymous
            ? new EndpointMetadataCollection(new AllowAnonymousAttribute())
            : EndpointMetadataCollection.Empty;

        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        return context;
    }

    private static (JwtValidationMiddleware Middleware, Func<bool> WasNextCalled) CreateMiddleware()
    {
        var nextCalled = false;
        var middleware = new JwtValidationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        return (middleware, () => nextCalled);
    }
}
