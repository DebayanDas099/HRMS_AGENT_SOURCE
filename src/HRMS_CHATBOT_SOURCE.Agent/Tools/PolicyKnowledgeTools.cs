using System.ComponentModel;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent.Tools;

/// <summary>
/// The Knowledge Agent's only tool: grounded retrieval over approved documents.
/// <para>
/// Deliberately read-only and non-transactional. The agent decides what to search
/// for and how to phrase the answer; it never decides what may be released. Filters
/// that gate visibility are applied server-side inside this tool, not passed in by
/// the model, so a prompt injection cannot widen them.
/// </para>
/// </summary>
public sealed class PolicyKnowledgeTools
{
    private const int MaxPassages = 5;

    /// <summary>Trimmed so several passages fit the model's context without crowding out history.</summary>
    private const int MaxPassageLength = 1200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PolicyKnowledgeTools> _logger;

    public PolicyKnowledgeTools(IServiceScopeFactory scopeFactory, ILogger<PolicyKnowledgeTools> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Description(
        "Searches the company's approved HR policy and training documents and returns the most "
        + "relevant passages with their source titles. Call this before answering any question "
        + "about policy, entitlements, procedures or company rules. Answer only from the passages "
        + "this returns; if it returns no passages or reports low confidence, say you could not "
        + "find an approved answer.")]
    public async Task<PolicySearchToolResult> SearchPolicyDocumentsAsync(
        [Description("The employee's question, in natural language. Include the key terms they used.")]
        string question,
        [Description("Optional filter. Use 'Policy' for company policy documents or 'Training' for training material. Leave empty to search both.")]
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return PolicySearchToolResult.NotFound("No question was supplied to search for.");
        }

        try
        {
            // The workflow factory is a singleton and retrieval is scoped, so a scope is
            // opened per invocation rather than capturing a scoped service in a singleton.
            using var scope = _scopeFactory.CreateScope();
            var search = scope.ServiceProvider.GetRequiredService<IVectorSearchService>();

            var response = await search.SearchAsync(
                new RetrievalQuery
                {
                    Query = question,
                    TopK = MaxPassages,
                    Category = NormaliseCategory(category),

                    // Server-side, not model-supplied: deactivated documents are never
                    // retrievable regardless of what the model asks for.
                    ActiveOnly = true
                },
                cancellationToken);

            if (response.Results.Count == 0)
            {
                _logger.LogInformation("Policy search for {Question} returned no passages.", question);
                return PolicySearchToolResult.NotFound(
                    "No approved document matched this question.");
            }

            var result = new PolicySearchToolResult
            {
                Grounded = response.IsConfident,
                Confidence = Math.Round(response.Confidence, 2),
                Passages = response.Results.Select(hit => new PolicyPassage
                {
                    Title = hit.Title,
                    Category = hit.Category,
                    Section = hit.SectionPath,
                    Text = Truncate(hit.Content, MaxPassageLength)
                }).ToList()
            };

            if (!response.IsConfident)
            {
                result.Guidance =
                    "Retrieval confidence is low. Tell the employee you could not find an approved "
                    + "answer rather than answering from these passages.";
            }

            _logger.LogInformation(
                "Policy search for {Question} returned {PassageCount} passages, confidence {Confidence:F2}.",
                question,
                result.Passages.Count,
                result.Confidence);

            return result;
        }
        catch (Exception ex)
        {
            // Surfaced to the model as a clean "unavailable" rather than an exception,
            // so a retrieval outage degrades into an honest reply instead of a failed turn.
            _logger.LogError(ex, "Policy search failed for {Question}.", question);
            return PolicySearchToolResult.NotFound(
                "The policy knowledge base could not be reached. Tell the employee the service is "
                + "temporarily unavailable and to try again shortly.");
        }
    }

    private static string? NormaliseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return null;
        }

        // The model may pass anything; only the known vocabulary is honoured, and an
        // unrecognised value widens to "search everything" rather than erroring the turn.
        return DocumentCategories.Allowed.FirstOrDefault(allowed =>
            string.Equals(allowed, category.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength), "...");
    }
}

public sealed class PolicySearchToolResult
{
    /// <summary>False when nothing was found or confidence was below the configured floor.</summary>
    public bool Grounded { get; set; }

    public double Confidence { get; set; }

    public List<PolicyPassage> Passages { get; set; } = [];

    /// <summary>Instruction to the model for the not-found and low-confidence paths.</summary>
    public string? Guidance { get; set; }

    internal static PolicySearchToolResult NotFound(string guidance)
    {
        return new PolicySearchToolResult
        {
            Grounded = false,
            Confidence = 0,
            Guidance = guidance
        };
    }
}

public sealed class PolicyPassage
{
    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    /// <summary>Heading breadcrumb from the source document, for precise citation.</summary>
    public string Section { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}
