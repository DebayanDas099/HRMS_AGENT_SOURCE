using System.ComponentModel;
using System.Text.RegularExpressions;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.Agent.Tools;

public sealed class DocumentAgentTools
{
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

        var normalizedSearchText = NormalizeDeliverySearchText(searchText);
        var primaryMatches = await documentLogic
            .GetDocumentMatchesBySimilarityAsync(normalizedSearchText, primaryMinScore, topCount, cancellationToken)
            .ConfigureAwait(false);

        if (primaryMatches.Count > 0)
        {
            return primaryMatches;
        }

        return await documentLogic
            .GetDocumentMatchesBySimilarityAsync(normalizedSearchText, fallbackMinScore, topCount, cancellationToken)
            .ConfigureAwait(false);
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

    private static string NormalizeDeliverySearchText(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return string.Empty;
        }

        var normalized = searchText.Trim();
        normalized = Regex.Replace(normalized, @"\bletest\b", "latest", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(
            normalized,
            @"\b(share|send|mail|email|over|document|doc|me|please)\b",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\blatest\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

        return string.IsNullOrWhiteSpace(normalized) ? searchText.Trim() : normalized;
    }
}
