using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Extensions;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Domain.Json;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using HRMS_CHATBOT_SOURCE.Infrastructure.Interfaces;
using HRMS_CHATBOT_SOURCE.Infrastructure.Services;
using MCC.Foundation.Authentication;
using MCC.Foundation.Chunker;
using MCC.Foundation.CosmosHelper;
using MCC.Foundation.MSSQLHelper.Extension;
using MCC.Foundation.QdrantHelper;
using MCC.Foundation.QdrantHelper.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace HRMS_CHATBOT_SOURCE.Infrastructure.Extensions;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHrmsCoreServices(
        this IServiceCollection services,
        IConfiguration configuration,
        ApplicationSecrets applicationSecrets)
    {
        services.Configure<AzureKeyVaultSettings>(configuration.GetSection(AzureKeyVaultSettings.SectionName));
        services.Configure<AppSettings>(configuration.GetSection(AppSettings.SectionName));

        services.AddHttpContextAccessor();
        services.AddMemoryCache();        services.AddScoped<IServiceContext, ServiceContext>();

        services.AddSingleton(applicationSecrets);
        services.AddSingleton<IAzureKeyVaultService, AzureKeyVaultService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        return services;
    }

    public static IServiceCollection AddHrmsMvc(this IServiceCollection services)    {
        services.AddControllersWithViews()
            .AddNewtonsoftJson(options =>
            {
                var jsonSettings = DomainJsonSerializerSettings.Default;
                options.SerializerSettings.ContractResolver = jsonSettings.ContractResolver;
                options.SerializerSettings.NullValueHandling = jsonSettings.NullValueHandling;
                options.SerializerSettings.ReferenceLoopHandling = jsonSettings.ReferenceLoopHandling;
            })
            .AddRazorOptions(options =>
            {
                options.ViewLocationFormats.Clear();
                options.ViewLocationFormats.Add("/web/Views/{1}/{0}.cshtml");
                options.ViewLocationFormats.Add("/web/Views/Shared/{0}.cshtml");
                options.AreaViewLocationFormats.Clear();
                options.AreaViewLocationFormats.Add("/web/Areas/{2}/Views/{1}/{0}.cshtml");
                options.AreaViewLocationFormats.Add("/web/Areas/{2}/Views/Shared/{0}.cshtml");
                options.AreaViewLocationFormats.Add("/web/Views/Shared/{0}.cshtml");
            });

        return services;
    }

    public static IServiceCollection AddHrmsCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>()
            ?? new CorsSettings();

        services.AddCors(options =>
        {
            options.AddPolicy("HrmsCorsPolicy", policy =>
            {
                if (corsSettings.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(corsSettings.AllowedOrigins);
                }
                else
                {
                    policy.SetIsOriginAllowed(_ => true);
                }

                policy.WithMethods(corsSettings.AllowedMethods);
                policy.WithHeaders(corsSettings.AllowedHeaders);

                if (corsSettings.AllowCredentials)
                {
                    policy.AllowCredentials();
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddHrmsSession(this IServiceCollection services, IConfiguration configuration)
    {
        var sessionSettings = configuration.GetSection(SessionSettings.SectionName).Get<SessionSettings>()
            ?? new SessionSettings();

        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(sessionSettings.IdleTimeoutMinutes);
            options.Cookie.Name = sessionSettings.CookieName;
            options.Cookie.HttpOnly = sessionSettings.CookieHttpOnly;
            options.Cookie.IsEssential = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        return services;
    }

    public static IServiceCollection AddHrmsFoundationServices(
        this IServiceCollection services,
        IConfiguration configuration,
        ApplicationSecrets applicationSecrets)
    {
        services.AddMSSQLHelperService();

        if (!string.IsNullOrWhiteSpace(applicationSecrets.CosmosSecret))
        {
            services.AddAzureCosmosService(applicationSecrets.CosmosSecret);
        }

        services.AddQdrantService(new QdrantConnectionModel
        {
            Host = applicationSecrets.QdrantEndpoint ?? string.Empty,
            ApiKey = applicationSecrets.QdrantApiKey ?? string.Empty
        });

        services.AddDocumentChunker(configuration);
        services.AddHrmsStorageServices(configuration);

        return services;
    }

    public static IServiceCollection AddHrmsAuthentication(        this IServiceCollection services,
        IHostEnvironment environment)
    {
        services.AddAuthentication(environment.IsProduction(), options =>
        {
            options.CookieName = "hrms_admin_token";
            options.HeaderName = "hrms_admin_token";
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
            {
                policy.RequireAuthenticatedUser();
            });
        });

        return services;
    }
    public static IServiceCollection AddHrmsSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        return services;
    }
}
