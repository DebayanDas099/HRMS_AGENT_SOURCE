using System.ComponentModel;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace HRMS_CHATBOT_SOURCE.Agent;

public interface IDocumentKernelFunctionCatalog
{
    IReadOnlyList<AITool> GetTools();
}

public sealed class DocumentKernelFunctionCatalog : IDocumentKernelFunctionCatalog
{
    private readonly DocumentKernelFunctions _kernelFunctions;
    private IReadOnlyList<AITool>? _cachedTools;

    public DocumentKernelFunctionCatalog(DocumentKernelFunctions kernelFunctions)
    {
        _kernelFunctions = kernelFunctions;
    }

    public IReadOnlyList<AITool> GetTools()
    {
        if (_cachedTools != null)
        {
            return _cachedTools;
        }

        _cachedTools =
        [
            AIFunctionFactory.Create(
                _kernelFunctions.SearchDocumentsBySimilarityAsync,
                nameof(DocumentKernelFunctions.SearchDocumentsBySimilarityAsync),
                "Search document_mstr by similarity score and return matching document ids."),
            AIFunctionFactory.Create(
                _kernelFunctions.BuildDocumentDownloadLink,
                nameof(DocumentKernelFunctions.BuildDocumentDownloadLink),
                "Build a download URL for a document id.")
        ];

        return _cachedTools;
    }
}

public sealed class DocumentKernelFunctions
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IChatTurnContextAccessor _chatTurnContextAccessor;
    private readonly AppSettings _appSettings;

    public DocumentKernelFunctions(
        IServiceScopeFactory scopeFactory,
        IChatTurnContextAccessor chatTurnContextAccessor,
        IOptions<AppSettings> appSettings)
    {
        _scopeFactory = scopeFactory;
        _chatTurnContextAccessor = chatTurnContextAccessor;
        _appSettings = appSettings.Value;
    }

    [KernelFunction]
    [Description("Searches document_mstr and returns the best document matches with similarity score.")]
    public async Task<IReadOnlyList<DocumentSimilarityMatchDto>> SearchDocumentsBySimilarityAsync(
        [Description("Document title or keyword provided by user.")] string searchText,
        [Description("Minimum acceptable similarity score from 0 to 100.")] int minScore = 65,
        [Description("Maximum number of matches to return.")] int topCount = 5)
    {
        using var scope = _scopeFactory.CreateScope();
        var documentLogic = scope.ServiceProvider.GetRequiredService<IDocumentLogic>();
        return await documentLogic
            .GetDocumentMatchesBySimilarityAsync(searchText, minScore, topCount)
            .ConfigureAwait(false);
    }

    [KernelFunction]
    [Description("Builds a secure document download URL from document id using the current chat user context.")]
    public string BuildDocumentDownloadLink(
        [Description("Document id to download.")] long documentId)
    {
        var mobile = _chatTurnContextAccessor.Mobile;
        var baseUrl = _chatTurnContextAccessor.BaseUrl;
        var encryptionKey = _appSettings.EncryptionKey;

        var token = DocumentDownloadTokenCodec.Encrypt(mobile, documentId, encryptionKey);
        var relativeUrl = string.IsNullOrWhiteSpace(token)
            ? string.Empty
            : $"/api/ChatDocumentDownload?token={token}";

        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return "Unable to generate secure download link.";
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return relativeUrl;
        }

        var normalizedBaseUrl = baseUrl.Trim().TrimEnd('/');
        return Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out _)
            ? normalizedBaseUrl + relativeUrl
            : relativeUrl;
    }
}
