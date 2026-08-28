using System.Text;
using Azure;
using Azure.AI.OpenAI;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Helpers;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Helpers;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace HRMS_CHATBOT_SOURCE.RAG.Services;

/// <summary>
/// Reranks fused candidates with the Azure AI Foundry chat deployment, standing in
/// for a dedicated cross-encoder.
/// <para>
/// Every failure path returns the input order unchanged. Reranking is a relevance
/// improvement, never a availability dependency.
/// </para>
/// </summary>
public class FoundryRerankService : IRerankService
{
    private readonly RetrievalSettings _settings;
    private readonly ILogger<FoundryRerankService> _logger;
    private readonly ChatClient? _chatClient;

    public FoundryRerankService(
        IOptions<AgentFoundrySettings> foundryOptions,
        IOptions<RetrievalSettings> retrievalOptions,
        IConfiguration configuration,
        ILogger<FoundryRerankService> logger)
    {
        _settings = retrievalOptions.Value;
        _logger = logger;

        try
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

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiVersion))
            {
                _logger.LogWarning("Foundry chat credentials are not configured; reranking is disabled.");
                return;
            }

            var deployment = AzureFoundryEndpointResolver.ResolveChatDeployment(foundry, configuration);
            var clientOptions = new AzureOpenAIClientOptions(
                Helpers.AzureOpenAiServiceVersionResolver.Resolve(apiVersion));

            var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey), clientOptions);
            _chatClient = client.GetChatClient(deployment);

            _logger.LogInformation("Rerank client configured for chat deployment {Deployment}.", deployment);
        }
        catch (Exception ex)
        {
            // A misconfigured reranker must not stop the app from serving retrieval.
            _logger.LogError(ex, "Failed to configure the rerank client; reranking is disabled.");
        }
    }

    public async Task<IReadOnlyList<RetrievalCandidate>> RerankAsync(
        string query,
        IReadOnlyList<RetrievalCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        if (_chatClient == null || candidates.Count <= 1)
        {
            return candidates;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _settings.RerankTimeoutSeconds)));

        try
        {
            var completion = await _chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(BuildSystemPrompt()),
                    new UserChatMessage(BuildUserPrompt(query, candidates))
                ],
                new ChatCompletionOptions { Temperature = 0f },
                timeout.Token);

            var content = completion.Value.Content.Count > 0
                ? completion.Value.Content[0].Text
                : null;

            var scores = RerankResponseParser.Parse(content);
            if (scores.Count == 0)
            {
                _logger.LogWarning("Reranker returned no usable scores; keeping fusion order.");
                return candidates;
            }

            return ApplyScores(candidates, scores);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Reranking timed out after {TimeoutSeconds}s; keeping fusion order.",
                _settings.RerankTimeoutSeconds);
            return candidates;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reranking failed; keeping fusion order.");
            return candidates;
        }
    }

    private IReadOnlyList<RetrievalCandidate> ApplyScores(
        IReadOnlyList<RetrievalCandidate> candidates,
        IReadOnlyDictionary<int, double> scores)
    {
        var maxFused = candidates.Max(candidate => candidate.FusedScore);
        var fusionWeight = 1.0 - _settings.RerankWeight;

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];

            if (!scores.TryGetValue(index, out var relevance))
            {
                // Unscored candidates keep fusion evidence only, so they sink below scored ones.
                candidate.RerankScore = null;
                candidate.FinalScore = fusionWeight * Normalise(candidate.FusedScore, maxFused);
                continue;
            }

            var normalisedRelevance = Math.Clamp(relevance, 0, 10) / 10.0;

            candidate.RerankScore = relevance;

            // Blended rather than replaced: a single bad model response should not be
            // able to discard all lexical and vector evidence.
            candidate.FinalScore = (_settings.RerankWeight * normalisedRelevance)
                + (fusionWeight * Normalise(candidate.FusedScore, maxFused));
        }

        return candidates
            .OrderByDescending(candidate => candidate.FinalScore)
            .ThenByDescending(candidate => candidate.FusedScore)
            .ToList();
    }

    private static double Normalise(double value, double max)
    {
        return max <= 0 ? 0 : value / max;
    }

    private static string BuildSystemPrompt()
    {
        return "You rank retrieved HR policy passages by how well they answer a question. "
            + "Score each passage 0-10, where 10 means it directly and completely answers the question "
            + "and 0 means it is irrelevant. Judge only the passage text; never use outside knowledge. "
            + "Respond with JSON only, in exactly this form: "
            + "{\"scores\":[{\"id\":0,\"relevance\":7}]}. "
            + "Include an entry for every passage id you were given and no others.";
    }

    private static string BuildUserPrompt(string query, IReadOnlyList<RetrievalCandidate> candidates)
    {
        var builder = new StringBuilder();
        builder.Append("Question: ").AppendLine(query).AppendLine();
        builder.AppendLine("Passages:");

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];

            builder.Append("[id ").Append(index).Append("] ").AppendLine(candidate.Title);

            if (!string.IsNullOrWhiteSpace(candidate.SectionPath))
            {
                builder.Append("Section: ").AppendLine(candidate.SectionPath);
            }

            builder.AppendLine(Truncate(candidate.Content, RetrievalDefaults.RerankSnippetLength));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength), "...");
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
