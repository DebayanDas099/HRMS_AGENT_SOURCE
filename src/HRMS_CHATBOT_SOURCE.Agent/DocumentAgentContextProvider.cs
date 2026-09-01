using System.Text;
using System.Text.RegularExpressions;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent;

public interface IDocumentAgentContextProvider
{
    Task<string?> BuildCurrentRepositoryNoticeAsync(
        string? userMessage,
        string? mobile,
        CancellationToken cancellationToken = default);

    Task<string?> BuildCurrentRepositoryNoticeAsync(
        string? userMessage,
        string? mobile,
        string? baseUrl,
        CancellationToken cancellationToken = default);
}

public sealed class DocumentAgentContextProvider : IDocumentAgentContextProvider
{
    private const int SnapshotPageSize = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentAgentContextProvider> _logger;

    public DocumentAgentContextProvider(
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentAgentContextProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<string?> BuildCurrentRepositoryNoticeAsync(
        string? userMessage,
        string? mobile,
        CancellationToken cancellationToken = default)
    {
        return await BuildCurrentRepositoryNoticeAsync(
                userMessage,
                mobile,
                baseUrl: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> BuildCurrentRepositoryNoticeAsync(
        string? userMessage,
        string? mobile,
        string? baseUrl,
        CancellationToken cancellationToken = default)
    {
        _ = mobile;
        _ = baseUrl;

        using var scope = _scopeFactory.CreateScope();
        var documentLogic = scope.ServiceProvider.GetService<IDocumentLogic>();
        if (documentLogic == null)
        {
            return null;
        }

        try
        {
            var statistics = await documentLogic.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
            var documents = await documentLogic
                .GetDocumentsAsync(
                    category: null,
                    searchText: null,
                    pageNumber: 1,
                    pageSize: SnapshotPageSize,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var builder = new StringBuilder();
            builder.AppendLine("Document repository snapshot for this turn (source of truth):");
            builder.AppendLine($"- Total documents: {statistics?.TotalDocuments ?? 0}");
            builder.AppendLine($"- Active documents: {statistics?.ActiveDocuments ?? 0}");
            builder.AppendLine($"- Inactive documents: {statistics?.InactiveDocuments ?? 0}");
            builder.AppendLine($"- Policy documents: {statistics?.PolicyDocuments ?? 0}");
            builder.AppendLine($"- Training documents: {statistics?.TrainingDocuments ?? 0}");
            builder.AppendLine("- Recent documents:");

            if (documents?.Items == null || documents.Items.Count == 0)
            {
                builder.Append("- None.");
                return builder.ToString();
            }

            foreach (var item in documents.Items)
            {
                var activeState = string.Equals(item.Active, "Y", StringComparison.OrdinalIgnoreCase)
                    ? "Active"
                    : "Inactive";
                builder.AppendLine(
                    $"- #{item.DocumentId}: {item.Name} | Category: {item.Category} | {activeState} | Ingestion: {item.IngestionStatus}");
            }

            var requestedDocumentId = ExtractRequestedDocumentId(userMessage);
            if (requestedDocumentId.HasValue)
            {
                var matched = documents.Items.FirstOrDefault(item => item.DocumentId == requestedDocumentId.Value);
                if (matched != null)
                {
                    var activeState = string.Equals(matched.Active, "Y", StringComparison.OrdinalIgnoreCase)
                        ? "Active"
                        : "Inactive";
                    builder.AppendLine();
                    builder.AppendLine("- Requested document focus:");
                    builder.Append(
                        $"- #{matched.DocumentId}: {matched.Name} | Category: {matched.Category} | {activeState} | Ingestion: {matched.IngestionStatus}");
                }
            }

            var requestedCategory = ExtractRequestedCategory(userMessage);
            if (!string.IsNullOrWhiteSpace(requestedCategory))
            {
                var categoryMatchCount = documents.Items.Count(item =>
                    string.Equals(item.Category, requestedCategory, StringComparison.OrdinalIgnoreCase));
                builder.AppendLine();
                builder.AppendLine("- Requested category focus:");
                builder.AppendLine($"- Category requested: {requestedCategory}");
                builder.Append($"- Matching items in this snapshot: {categoryMatchCount}");
            }

            return builder.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build DocumentAgent repository snapshot.");
            return "Document repository snapshot is temporarily unavailable for this turn.";
        }
    }

    private static long? ExtractRequestedDocumentId(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return null;
        }

        var idMatch = Regex.Match(
            userMessage,
            @"\b(?:document|doc|file)\s*(?:id)?\s*[:#]?\s*(\d{1,12})\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!idMatch.Success)
        {
            return null;
        }

        return long.TryParse(idMatch.Groups[1].Value, out var id) && id > 0
            ? id
            : null;
    }

    private static string? ExtractRequestedCategory(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return null;
        }

        if (Regex.IsMatch(userMessage, @"\bpolicy\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return "Policy";
        }

        if (Regex.IsMatch(userMessage, @"\btraining\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return "Training";
        }

        return null;
    }

}
