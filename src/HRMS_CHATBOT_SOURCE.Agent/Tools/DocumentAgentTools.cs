using System.ComponentModel;
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

    [Description("Builds a secure document download URL for a document id using current chat context.")]
    public string BuildDocumentDownloadLink(
        [Description("Document id to download.")] long documentId)
    {
        using var scope = _scopeFactory.CreateScope();
        var documentLogic = scope.ServiceProvider.GetRequiredService<IDocumentLogic>();
        return documentLogic.BuildDocumentDownloadLink(documentId);
    }
}
