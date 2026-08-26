using MCC.Foundation.MSSQLHelper.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Core;

public interface IServiceContext
{
    IConfiguration Configuration { get; }
    IWebHostEnvironment CurrentEnvironment { get; }
    HttpContext? RequestContext { get; }
    IMemoryCache MemoryCache { get; }
    bool IsProduction { get; }
    int RequestTimeout { get; }
    string ContentRootPath { get; }
    string? RequestTraceId { get; }
    CurrentUserContext? CurrentUser { get; }
    string? IpAddress { get; }
    string? HostUrl { get; }
    MSSQLConnectionModel SQLConnectionModel { get; }
}

public class CurrentUserContext
{
    public string? UserId { get; set; }
    public string? FullName { get; set; }
    public string? GroupCode { get; set; }
    public string? Department { get; set; }
    public string? Designation { get; set; }
    public string? IsAdmin { get; set; }
}
