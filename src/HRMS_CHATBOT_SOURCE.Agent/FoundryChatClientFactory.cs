using Azure;
using Azure.AI.OpenAI;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Agent;

internal static class FoundryChatClientFactory
{
    public static IChatClient Create(
        IOptions<AgentFoundrySettings> foundryOptions,
        IConfiguration configuration,
        ILogger logger)
    {
        var foundry = foundryOptions.Value;
        var endpoint = AzureFoundryEndpointResolver.ResolveOpenAIEndpoint(configuration, foundry);
        var apiKey = FirstNonEmpty(
            foundry.ApiKey,
            configuration["AgentFoundry:ApiKey"],
            configuration["LLM:ApiKey"]);
        var apiVersion = FirstNonEmpty(
            foundry.ApiVersion,
            configuration["AgentFoundry:ApiVersion"],
            configuration["LLM:ApiVersion"]);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("AgentFoundry API key is not configured.");
        }

        var deployment = AzureFoundryEndpointResolver.ResolveChatDeployment(foundry, configuration);
        var clientOptions = new AzureOpenAIClientOptions(ResolveServiceVersion(apiVersion));
        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey), clientOptions);
        // Function invocation middleware is what actually executes a tool the model asks
        // for and feeds the result back. Without it, attached tools are advertised to the
        // model and then silently never run.
        var chatClient = azureClient.GetChatClient(deployment)
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();

        logger.LogInformation("HRMS chat client configured for deployment {Deployment}.", deployment);
        return chatClient;
    }

    private static AzureOpenAIClientOptions.ServiceVersion ResolveServiceVersion(string? apiVersion)
    {
        if (string.IsNullOrWhiteSpace(apiVersion)
            || string.Equals(apiVersion, "2024-10-21", StringComparison.OrdinalIgnoreCase)
            || string.Equals(apiVersion, "V2024_10_21", StringComparison.OrdinalIgnoreCase))
        {
            return AzureOpenAIClientOptions.ServiceVersion.V2024_10_21;
        }

        if (string.Equals(apiVersion, "2024-06-01", StringComparison.OrdinalIgnoreCase)
            || string.Equals(apiVersion, "V2024_06_01", StringComparison.OrdinalIgnoreCase))
        {
            return AzureOpenAIClientOptions.ServiceVersion.V2024_06_01;
        }

        return AzureOpenAIClientOptions.ServiceVersion.V2024_10_21;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
