namespace HRMS_CHATBOT_SOURCE.Domain.Constants;

public static class KeyVaultSecretNames
{
    public const string AzureSqlSecret = "Azure-SQL-Secret";
    public const string CosmosSecret = "Cosmos-Secret";
    public const string DocumentIntelligenceApiKey = "Document-Intelligence-API-Key";
    public const string DocumentIntelligenceEndpoint = "Document-Intelligence-Endpoint";
    public const string LlmApiKey = "LLM-API-Key";
    public const string LlmEndpoint = "LLM-Endpoint";
    public const string LlmOpenAiEndpoint = "LLM-Endpoint";
    public const string LlmApiVersion = "LLM-Version";
    public const string LlmDeployment = "LLM-Deployment";
    public const string LlmEmbeddingDeployment = "LLM-Embedding-Deployment";
    public const string QdrantApiKey = "Qdrant-API-Key";
    public const string QdrantEndpoint = "Qdrant-Endpoint";
    public const string AzureAiSearchAdminKey = "Azure-AI-Search-Admin-Key";
    public const string AzureAiSearchEndpoint = "Azure-AI-Search-Endpoint";
    public const string AzureSpeechApiKey = "Azure-Speech-API-Key";
    public const string AzureSpeechEndpoint = "Azure-Speech-Endpoint";

    public static readonly string[] All =
    [
        AzureSqlSecret,
        CosmosSecret,
        DocumentIntelligenceApiKey,
        DocumentIntelligenceEndpoint,
        LlmApiKey,
        LlmEndpoint,
        LlmOpenAiEndpoint,
        LlmApiVersion,
        LlmDeployment,
        LlmEmbeddingDeployment,
        QdrantApiKey,
        QdrantEndpoint,
        AzureAiSearchAdminKey,
        AzureAiSearchEndpoint,
        AzureSpeechApiKey,
        AzureSpeechEndpoint
    ];
}
