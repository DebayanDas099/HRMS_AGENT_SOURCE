using System.ComponentModel;
using System.Text.RegularExpressions;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic;
using HRMS_CHATBOT_SOURCE.Logic.Common;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.Agent.Tools;

public sealed class DocumentAgentTools
{
    private enum DeliveryPreference
    {
        None,
        Latest,
        Oldest
    }

    private readonly IServiceScopeFactory _scopeFactory;

    public DocumentAgentTools(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [Description("Searches document_mstr by similarity and returns matching document ids with scores.")]
    public async Task<IReadOnlyList<DocumentSimilarityMatchDto>> SearchDocumentsBySimilarityAsync(
        [Description("Document title or keyword provided by user.")] string searchText,
        [Description("Minimum acceptable similarity score from 0 to 100.")] int minScore = 65,
        [Description("Maximum number of matches to return.")] int topCount = 5,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var documentLogic = scope.ServiceProvider.GetRequiredService<IDocumentLogic>();
        return await documentLogic
            .GetDocumentMatchesBySimilarityAsync(searchText, minScore, topCount, cancellationToken)
            .ConfigureAwait(false);
    }

    [Description("Resolves documents for download or email delivery with a fallback search: first strict score, then lower score when no strict match is found.")]
    public async Task<IReadOnlyList<DocumentSimilarityMatchDto>> ResolveDocumentsForDeliveryAsync(
        [Description("User message containing requested document name.")] string searchText,
        [Description("Primary minimum similarity score from 0 to 100.")] int primaryMinScore = 65,
        [Description("Fallback minimum similarity score from 0 to 100 when primary search returns nothing.")] int fallbackMinScore = 10,
        [Description("Maximum number of matches to return.")] int topCount = 5,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var documentLogic = scope.ServiceProvider.GetRequiredService<IDocumentLogic>();
        var preference = DetectDeliveryPreference(searchText);

        var normalizedSearchText = NormalizeDeliverySearchText(searchText);
        var primaryMatches = await documentLogic
            .GetDocumentMatchesBySimilarityAsync(normalizedSearchText, primaryMinScore, topCount, cancellationToken)
            .ConfigureAwait(false);

        if (primaryMatches.Count > 0)
        {
            return ApplyPreferenceOrder(primaryMatches, preference);
        }

        var fallbackMatches = await documentLogic
            .GetDocumentMatchesBySimilarityAsync(normalizedSearchText, fallbackMinScore, topCount, cancellationToken)
            .ConfigureAwait(false);
        return ApplyPreferenceOrder(fallbackMatches, preference);
    }

    [Description("Builds a secure document download URL for a document id using current chat context.")]
    public string BuildDocumentDownloadLink(
        [Description("Document id to download.")] long documentId)
    {
        using var scope = _scopeFactory.CreateScope();
        var documentLogic = scope.ServiceProvider.GetRequiredService<IDocumentLogic>();
        return documentLogic.BuildDocumentDownloadLink(documentId);
    }

    [Description("Sends the resolved document download link to the logged-in user's registered email address.")]
    public async Task<string> SendDocumentLinkByMailAsync(
        [Description("Document id to send by email.")] long documentId,
        [Description("Optional document name for email subject/body.")] string? documentName = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var documentLogic = scope.ServiceProvider.GetRequiredService<IDocumentLogic>();
        return await documentLogic
            .SendDocumentLinkByMailAsync(documentId, documentName, cancellationToken)
            .ConfigureAwait(false);
    }

    [Description("Formats a draft DocumentAgent reply into a clean, structured, readable response. Call this before sending the final user-facing reply.")]
    public string FormatDocumentAgentReply(
        [Description("Draft reply text to format for final response.")] string draftReply)
    {
        using var scope = _scopeFactory.CreateScope();
        var commonLogic = scope.ServiceProvider.GetRequiredService<ICommonLogic>();
        return commonLogic.FormatAgentReply(draftReply);
    }

    private static string NormalizeDeliverySearchText(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return string.Empty;
        }

        var normalized = searchText.Trim();
        normalized = Regex.Replace(normalized, @"\bletest\b", "latest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\blatset\b", "latest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\blattest\b", "latest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\boldset\b", "oldest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\boldst\b", "oldest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(
            normalized,
            @"\b(share|send|mail|email|over|document|doc|me|please)\b",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(
            normalized,
            @"\b(latest|newest|recent|oldest|earliest|old)\b",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

        return string.IsNullOrWhiteSpace(normalized) ? searchText.Trim() : normalized;
    }

    private static DeliveryPreference DetectDeliveryPreference(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return DeliveryPreference.None;
        }

        var normalized = searchText.Trim();
        normalized = Regex.Replace(normalized, @"\bletest\b|\blatset\b|\blattest\b", "latest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\boldset\b|\boldst\b", "oldest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (Regex.IsMatch(normalized, @"\b(latest|newest|recent)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return DeliveryPreference.Latest;
        }

        if (Regex.IsMatch(normalized, @"\b(oldest|earliest|old)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return DeliveryPreference.Oldest;
        }

        return DeliveryPreference.None;
    }

    private static IReadOnlyList<DocumentSimilarityMatchDto> ApplyPreferenceOrder(
        IReadOnlyList<DocumentSimilarityMatchDto> matches,
        DeliveryPreference preference)
    {
        if (matches.Count <= 1 || preference == DeliveryPreference.None)
        {
            return matches;
        }

        return preference == DeliveryPreference.Latest
            ? matches
                .OrderByDescending(match => match.CreatedDate ?? DateTime.MinValue)
                .ThenByDescending(match => match.DocumentId)
                .ToList()
            : matches
                .OrderBy(match => match.CreatedDate ?? DateTime.MaxValue)
                .ThenBy(match => match.DocumentId)
                .ToList();
    }
}
