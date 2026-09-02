using HRMS_CHATBOT_SOURCE.Agent.Tools;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class DocumentAgentToolsTests
{
    [Fact]
    public void BuildDocumentDownloadLink_DelegatesToDocumentLogic()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDocumentLogic>(_ => new FakeDocumentLogic());
        using var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var tools = new DocumentAgentTools(scopeFactory);

        var link = tools.BuildDocumentDownloadLink(13);

        Assert.Equal("https://localhost:7249/api/ChatDocumentDownload?token=test-token", link);
    }

    private sealed class FakeDocumentLogic : IDocumentLogic
    {
        public void SetChatTurnContext(string? mobile, string? baseUrl)
        {
        }

        public void ClearChatTurnContext()
        {
        }

        public Task<DocumentStatisticsDto?> GetStatisticsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentStatisticsDto?>(null);

        public Task<DocumentListResponseDto?> GetDocumentsAsync(
            string? category,
            string? searchText,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentListResponseDto?>(null);

        public Task<BulkDocumentUploadResponse?> BulkUploadAsync(
            string? category,
            IReadOnlyList<IFormFile> files,
            string? title,
            string? createdBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<BulkDocumentUploadResponse?>(null);

        public Task<DocumentMstrDto?> UpdateActiveAsync(long documentId, bool isActive, CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentMstrDto?>(null);

        public Task<IngestionResultDto?> IngestDocumentAsync(long documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<IngestionResultDto?>(null);

        public Task<DeleteDocumentResponse?> DeleteDocumentAsync(long documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<DeleteDocumentResponse?>(null);

        public Task<DocumentDownloadResult?> DownloadDocumentAsync(long documentId, CancellationToken cancellationToken = default)
            => Task.FromResult<DocumentDownloadResult?>(null);

        public Task<IReadOnlyList<DocumentSimilarityMatchDto>> GetDocumentMatchesBySimilarityAsync(
            string searchText,
            int minScore = 65,
            int topCount = 5,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DocumentSimilarityMatchDto>>([]);

        public string BuildDocumentDownloadLink(long documentId)
            => $"https://localhost:7249/api/ChatDocumentDownload?token=test-token";

        public bool TryDecodeDocumentDownloadToken(string? token, out long documentId)
        {
            documentId = 13;
            return true;
        }
    }
}
