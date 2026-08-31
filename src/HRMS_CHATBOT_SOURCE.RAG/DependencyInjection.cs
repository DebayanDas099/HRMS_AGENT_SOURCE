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
        services.Configure<RetrievalSettings>(configuration.GetSection(RetrievalSettings.SectionName));

        services.AddHrmsQdrantClient(configuration);

        // Singleton: only one IndexWriter may hold the Lucene directory lock, and opening
        // the directory is expensive. Registered by concrete type with the interface
        // delegating to it, so the container disposes exactly one instance.
        services.AddSingleton<LuceneLexicalIndex>();
        services.AddSingleton<ILexicalIndex>(provider => provider.GetRequiredService<LuceneLexicalIndex>());

        services.AddScoped<IEmbeddingService, AzureFoundryEmbeddingService>();
        services.AddScoped<IVectorStoreService, QdrantVectorStoreService>();
        services.AddScoped<IDocumentIngestionPipeline, DocumentIngestionPipeline>();
        services.AddScoped<IRerankService, FoundryRerankService>();
        services.AddScoped<LuceneIndexRebuilder>();
        services.AddScoped<IVectorSearchService, HybridRetrievalService>();

        return services;
    }
}
