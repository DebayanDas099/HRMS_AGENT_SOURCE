using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Domain.Services;
using MCC.Foundation.StorageManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.Domain.Extensions;

public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddHrmsStorageServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StorageSettings>(configuration.GetSection(StorageSettings.SectionName));
        services.AddAzureStorageService();
        services.AddScoped<IDocumentBlobService, DocumentBlobService>();

        return services;
    }
}
