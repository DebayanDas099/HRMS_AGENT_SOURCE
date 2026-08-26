using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Extensions;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.RAG;

public static class DependencyInjection
{
    public static IServiceCollection AddRag(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VectorStoreSettings>(configuration.GetSection(VectorStoreSettings.SectionName));
        services.Configure<AgentFoundrySettings>(configuration.GetSection(AgentFoundrySettings.SectionName));
        services.Configure<IngestionSettings>(configuration.GetSection(IngestionSettings.SectionName));

        services.AddHrmsQdrantClient(configuration);

        services.AddScoped<IEmbeddingService, AzureFoundryEmbeddingService>();
        services.AddScoped<IVectorStoreService, QdrantVectorStoreService>();
        services.AddScoped<IDocumentIngestionPipeline, DocumentIngestionPipeline>();

        return services;
    }
}
