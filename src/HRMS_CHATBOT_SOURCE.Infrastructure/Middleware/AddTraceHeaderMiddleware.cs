using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Middleware;

public class AddTraceHeaderMiddleware
{
    private readonly RequestDelegate _next;

    public AddTraceHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("X-Request-TraceId"))
            {
                context.Response.Headers.Append("X-Request-TraceId", context.TraceIdentifier);
            }

            if (!context.Response.Headers.ContainsKey("X-Request-ServedBy"))
            {
                context.Response.Headers.Append("X-Request-ServedBy", "HRMS_CHATBOT_SOURCE");
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

public static class AddTraceHeaderMiddlewareExtensions
{
    public static IApplicationBuilder UseHrmsTraceHeader(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AddTraceHeaderMiddleware>();
    }
}
