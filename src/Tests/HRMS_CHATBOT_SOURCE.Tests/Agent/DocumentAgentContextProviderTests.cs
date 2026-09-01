using HRMS_CHATBOT_SOURCE.Agent;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class DocumentAgentContextProviderTests
{
    [Fact]
    public async Task BuildCurrentRepositoryNoticeAsync_IncludesStatisticsAndRecentDocuments()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDocumentLogic>(_ => new FakeDocumentLogic());
        services.AddSingleton<IDocumentAgentContextProvider, DocumentAgentContextProvider>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DocumentAgentContextProvider>>(
            NullLogger<DocumentAgentContextProvider>.Instance);
        using var provider = services.BuildServiceProvider();

        var contextProvider = provider.GetRequiredService<IDocumentAgentContextProvider>();
        var notice = await contextProvider.BuildCurrentRepositoryNoticeAsync("show latest documents", "9999999999");

        Assert.NotNull(notice);
        Assert.Contains("Total documents: 2", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#10: Leave Policy 2026", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ingestion: Completed", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildCurrentRepositoryNoticeAsync_WithBaseUrl_UsesAbsoluteDownloadLinks()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDocumentLogic>(_ => new FakeDocumentLogic());
        services.AddSingleton<IDocumentAgentContextProvider, DocumentAgentContextProvider>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DocumentAgentContextProvider>>(
            NullLogger<DocumentAgentContextProvider>.Instance);
        using var provider = services.BuildServiceProvider();

        var contextProvider = provider.GetRequiredService<IDocumentAgentContextProvider>();
        var notice = await contextProvider.BuildCurrentRepositoryNoticeAsync(
            "download document id 10",
            "1234567890",
            "https://localhost:7249");

        Assert.NotNull(notice);
        Assert.Contains("Document repository snapshot for this turn", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildCurrentRepositoryNoticeAsync_WithDocumentIdAndCategory_AddsFocusSection()
    {
        var services = new ServiceCollection();
        services.AddScoped<IDocumentLogic>(_ => new FakeDocumentLogic());
        services.AddSingleton<IDocumentAgentContextProvider, DocumentAgentContextProvider>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DocumentAgentContextProvider>>(
            NullLogger<DocumentAgentContextProvider>.Instance);
        using var provider = services.BuildServiceProvider();

        var contextProvider = provider.GetRequiredService<IDocumentAgentContextProvider>();
        var notice = await contextProvider.BuildCurrentRepositoryNoticeAsync("show document id 10 policy status", "8888888888");

        Assert.NotNull(notice);
        Assert.Contains("Requested document focus", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#10: Leave Policy 2026", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Requested category focus", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Category requested: Policy", notice, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeDocumentLogic : IDocumentLogic
    {
        public Task<DocumentStatisticsDto?> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DocumentStatisticsDto?>(new DocumentStatisticsDto
            {
                TotalDocuments = 2,
                ActiveDocuments = 1,
                InactiveDocuments = 1,
                PolicyDocuments = 1,
                TrainingDocuments = 1
            });
        }

        public Task<DocumentListResponseDto?> GetDocumentsAsync(
            string? category,
            string? searchText,
            int pageNumber = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DocumentListResponseDto?>(new DocumentListResponseDto
            {
                Items =
                [
                    new DocumentMstrDto
                    {
                        DocumentId = 10,
                        Name = "Leave Policy 2026",
                        Category = "Policy",
                        Active = "Y",
                        IngestionStatus = "Completed"
                    },
                    new DocumentMstrDto
                    {
                        DocumentId = 11,
                        Name = "Onboarding Deck",
                        Category = "Training",
                        Active = "N",
                        IngestionStatus = "Pending"
                    }
                ]
            });
        }

        public Task<IReadOnlyList<DocumentSimilarityMatchDto>> GetDocumentMatchesBySimilarityAsync(
            string searchText,
            int minScore = 65,
            int topCount = 5,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<DocumentSimilarityMatchDto>>([]);
        }

        public Task<BulkDocumentUploadResponse?> BulkUploadAsync(
            string? category,
            IReadOnlyList<IFormFile> files,
            string? title,
            string? createdBy,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DocumentMstrDto?> UpdateActiveAsync(long documentId, bool isActive, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IngestionResultDto?> IngestDocumentAsync(long documentId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DeleteDocumentResponse?> DeleteDocumentAsync(long documentId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DocumentDownloadResult?> DownloadDocumentAsync(long documentId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
