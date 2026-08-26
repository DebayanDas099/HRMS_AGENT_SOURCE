using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Extensions;

public static class ApplicationSecretsExtensions
{
    public static IDictionary<string, string?> ToConfigurationDictionary(this ApplicationSecrets secrets)
    {
        var values = new Dictionary<string, string?>();

        AddIfPresent(values, "ConnectionStrings:HrmsDb", secrets.AzureSqlSecret);
        AddIfPresent(values, "ConnectionStrings:CosmosDb", secrets.CosmosSecret);
        AddIfPresent(values, "ConnectionStrings:AzureSql", secrets.AzureSqlSecret);
        AddIfPresent(values, "DocumentIntelligence:ApiKey", secrets.DocumentIntelligenceApiKey);
        AddIfPresent(values, "DocumentIntelligence:Endpoint", secrets.DocumentIntelligenceEndpoint);
        AddIfPresent(values, "LLM:ApiKey", secrets.LlmApiKey);
        AddIfPresent(values, "LLM:Endpoint", secrets.LlmEndpoint);
        AddIfPresent(values, "LLM:OpenAIEndpoint", secrets.LlmOpenAiEndpoint);
        AddIfPresent(values, "LLM:ApiVersion", secrets.LlmApiVersion);
        AddIfPresent(values, "LLM:Deployment", secrets.LlmDeployment);
        AddIfPresent(values, "AgentFoundry:ApiKey", secrets.LlmApiKey);
        AddIfPresent(values, "AgentFoundry:ProjectEndpoint", secrets.LlmEndpoint);
        AddIfPresent(values, "AgentFoundry:OpenAIEndpoint", secrets.LlmOpenAiEndpoint);
        AddIfPresent(values, "AgentFoundry:ApiVersion", secrets.LlmApiVersion);
        AddIfPresent(values, "AgentFoundry:ChatDeployment", secrets.LlmDeployment);
        AddIfPresent(values, "AgentFoundry:EmbeddingDeployment", secrets.LlmEmbeddingDeployment);
        AddIfPresent(values, "LLM:EmbeddingDeployment", secrets.LlmEmbeddingDeployment);
        AddIfPresent(values, "Qdrant:ApiKey", secrets.QdrantApiKey);
        AddIfPresent(values, "Qdrant:Endpoint", secrets.QdrantEndpoint);
        AddIfPresent(values, "Qdrant:Host", secrets.QdrantEndpoint);

        return values;
    }

    private static void AddIfPresent(IDictionary<string, string?> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value;
        }
    }
}
