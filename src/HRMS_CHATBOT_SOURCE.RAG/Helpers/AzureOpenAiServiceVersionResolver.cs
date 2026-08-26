using Azure.AI.OpenAI;

namespace HRMS_CHATBOT_SOURCE.RAG.Helpers;

internal static class AzureOpenAiServiceVersionResolver
{
    public static AzureOpenAIClientOptions.ServiceVersion Resolve(string? apiVersion)
    {
        if (string.IsNullOrWhiteSpace(apiVersion))
        {
            return AzureOpenAIClientOptions.ServiceVersion.V2024_10_21;
        }

        var normalized = apiVersion.Trim();
        if (VersionMap.TryGetValue(normalized, out var mapped))
        {
            return mapped;
        }

        if (Enum.TryParse<AzureOpenAIClientOptions.ServiceVersion>(normalized, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Unsupported AgentFoundry:ApiVersion '{apiVersion}'. Supported values include {string.Join(", ", VersionMap.Keys)}.");
    }

    private static readonly Dictionary<string, AzureOpenAIClientOptions.ServiceVersion> VersionMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["2024-06-01"] = AzureOpenAIClientOptions.ServiceVersion.V2024_06_01,
            ["2024-10-21"] = AzureOpenAIClientOptions.ServiceVersion.V2024_10_21,
            ["V2024_06_01"] = AzureOpenAIClientOptions.ServiceVersion.V2024_06_01,
            ["V2024_10_21"] = AzureOpenAIClientOptions.ServiceVersion.V2024_10_21
        };
}
