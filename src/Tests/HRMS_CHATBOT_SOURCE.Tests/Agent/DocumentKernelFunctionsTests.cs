using HRMS_CHATBOT_SOURCE.Agent;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class DocumentKernelFunctionsTests
{
    [Fact]
    public void BuildDocumentDownloadLink_UsesCurrentChatContextForMobileAndBaseUrl()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IChatTurnContextAccessor, ChatTurnContextAccessor>();
        services.AddScoped<IDocumentLogic>(_ => new FakeDocumentLogic());
        using var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var accessor = provider.GetRequiredService<IChatTurnContextAccessor>();
        var appSettings = Options.Create(new AppSettings { EncryptionKey = "unit-test-key" });
        var functions = new DocumentKernelFunctions(scopeFactory, accessor, appSettings);

        accessor.Set("1234567890", "https://localhost:7249");
        var link = functions.BuildDocumentDownloadLink(13);
        accessor.Clear();

        Assert.StartsWith("https://localhost:7249/api/ChatDocumentDownload?token=", link, StringComparison.Ordinal);

        var token = link.Split("token=", StringSplitOptions.None)[1];
        var isValid = DocumentDownloadTokenCodec.TryDecrypt(token, "unit-test-key", out var mobile, out var documentId);
        Assert.True(isValid);
        Assert.Equal("1234567890", mobile);
        Assert.Equal(13, documentId);
    }

    private sealed class FakeDocumentLogic : IDocumentLogic
    {
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
    }
}
