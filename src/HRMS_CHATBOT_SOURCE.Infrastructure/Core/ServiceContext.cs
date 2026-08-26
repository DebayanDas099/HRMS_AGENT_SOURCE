using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using MCC.Foundation.MSSQLHelper.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Core;

public class ServiceContext : IServiceContext
{
    public ServiceContext(
        IConfiguration configuration,
        ApplicationSecrets applicationSecrets,
        IWebHostEnvironment hostingEnvironment,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache memoryCache)
    {
        Configuration = configuration;
        CurrentEnvironment = hostingEnvironment;
        RequestContext = httpContextAccessor.HttpContext;
        MemoryCache = memoryCache;
        IsProduction = hostingEnvironment.IsProduction();
        RequestTimeout = configuration.GetValue("AppSettings:RequestTimeoutInSecond", 60);
        ContentRootPath = hostingEnvironment.ContentRootPath;
        RequestTraceId = RequestContext?.TraceIdentifier;
        CurrentUser = BuildCurrentUser(RequestContext);
        IpAddress = ResolveClientIpAddress(RequestContext);
        HostUrl = ResolveHostUrl(RequestContext);
        SQLConnectionModel = BuildSqlConnectionModel(configuration, applicationSecrets);
    }

    public IConfiguration Configuration { get; }
    public IWebHostEnvironment CurrentEnvironment { get; }
    public HttpContext? RequestContext { get; }
    public IMemoryCache MemoryCache { get; }
    public bool IsProduction { get; }
    public int RequestTimeout { get; }
    public string ContentRootPath { get; }
    public string? RequestTraceId { get; }
    public CurrentUserContext? CurrentUser { get; }
    public string? IpAddress { get; }
    public string? HostUrl { get; }
    public MSSQLConnectionModel SQLConnectionModel { get; }

    public static string? TryGetUserIdFromHttpContext(HttpContext? context)
    {
        if (context == null)
        {
            return null;
        }

        var claimUserId = context.User.FindFirst("UserId")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst(AdminClaimTypes.EmployeeId)?.Value;

        if (!string.IsNullOrWhiteSpace(claimUserId))
        {
            return claimUserId;
        }

        var token = ReadTokenFromHttpContext(context);
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
            return jwt.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, "UserId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase))?.Value;
        }
        catch
        {
            return null;
        }
    }

    public static string? GetClientIpAddress(HttpContext? context)
    {
        if (context == null)
        {
            return null;
        }

        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded)
            && !string.IsNullOrWhiteSpace(forwarded))
        {
            var first = forwarded.ToString()
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(first))
            {
                return NormalizeClientIp(first);
            }
        }

        var remote = context.Connection.RemoteIpAddress;
        if (remote == null)
        {
            return null;
        }

        return IPAddress.IsLoopback(remote) ? "127.0.0.1" : remote.ToString();
    }

    private static CurrentUserContext? BuildCurrentUser(HttpContext? context)
    {
        if (context?.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return new CurrentUserContext
        {
            UserId = context.User.FindFirst("UserId")?.Value
                ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            FullName = context.User.FindFirst("UserName")?.Value
                ?? context.User.FindFirst(ClaimTypes.Name)?.Value,
            GroupCode = context.User.FindFirst("UserGroup")?.Value
                ?? context.User.FindFirst(AdminClaimTypes.GroupCode)?.Value,
            Department = context.User.FindFirst(AdminClaimTypes.Department)?.Value,
            Designation = context.User.FindFirst(AdminClaimTypes.Designation)?.Value,
            IsAdmin = context.User.FindFirst(AdminClaimTypes.IsAdmin)?.Value
        };
    }

    private static MSSQLConnectionModel BuildSqlConnectionModel(
        IConfiguration configuration,
        ApplicationSecrets applicationSecrets)
    {
        var connectionString = applicationSecrets.AzureSqlSecret
            ?? configuration.GetConnectionString("HrmsDb")
            ?? throw new InvalidOperationException(
                $"SQL connection string is not configured. Provide Azure Key Vault secret '{KeyVaultSecretNames.AzureSqlSecret}' or ConnectionStrings:HrmsDb for local development.");

        var builder = new SqlConnectionStringBuilder(connectionString);

        return new MSSQLConnectionModel
        {
            ServerName = builder.DataSource,
            DataBaseName = builder.InitialCatalog,
            UserId = builder.UserID,
            Password = builder.Password,
            ConnectionRetryCount = 3,
            ConnectionRetryInterval = 2,
            ConnectionTimeout = Common.SQLCommandTimeOut
        };
    }

    private static string? NormalizeStoredToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        token = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? token[7..].Trim()
            : token.Trim();

        var suffixIndex = token.IndexOf("|@|", StringComparison.Ordinal);
        return suffixIndex > 0 ? token[..suffixIndex] : token;
    }

    private static string? ReadTokenFromHttpContext(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("hrms_admin_token", out var headerToken)
            && !string.IsNullOrWhiteSpace(headerToken))
        {
            return NormalizeStoredToken(headerToken.ToString());
        }

        if (context.Request.Headers.TryGetValue("Authorization", out var authorization)
            && !string.IsNullOrWhiteSpace(authorization))
        {
            return NormalizeStoredToken(authorization.ToString());
        }

        if (context.Request.Cookies.TryGetValue("hrms_admin_token", out var cookieToken)
            && !string.IsNullOrWhiteSpace(cookieToken))
        {
            return NormalizeStoredToken(cookieToken);
        }

        return null;
    }

    private static string? ResolveClientIpAddress(HttpContext? context) => GetClientIpAddress(context);

    private static string? ResolveHostUrl(HttpContext? context)
    {
        if (context?.Request == null)
        {
            return null;
        }

        return $"{context.Request.Scheme}://{context.Request.Host}";
    }

    private static string NormalizeClientIp(string ip)
    {
        if (string.Equals(ip, "::1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ip, "0:0:0:0:0:0:0:1", StringComparison.OrdinalIgnoreCase))
        {
            return "127.0.0.1";
        }

        return ip.Trim();
    }
}
