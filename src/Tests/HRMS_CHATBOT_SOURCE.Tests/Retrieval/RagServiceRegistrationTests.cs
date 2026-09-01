using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.RAG;
using HRMS_CHATBOT_SOURCE.RAG.Abstractions;
using HRMS_CHATBOT_SOURCE.RAG.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Tests.Retrieval;

/// <summary>
/// A missing or mis-scoped registration in AddRag would otherwise surface only as a
/// runtime failure on the first search request.
/// </summary>
public class RagServiceRegistrationTests
{
    [Fact]
    public void AddRag_BuildsAValidServiceGraph()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider);
    }

    [Fact]
    public void AddRag_RegistersOneLexicalIndexInstanceAsSingleton()
    {
        // Only one IndexWriter may hold the Lucene directory lock, so the concrete type
        // and the interface must resolve to the same singleton.
        using var provider = BuildProvider();
        using var scopeOne = provider.CreateScope();
        using var scopeTwo = provider.CreateScope();

        var byInterface = scopeOne.ServiceProvider.GetRequiredService<ILexicalIndex>();
        var byType = scopeOne.ServiceProvider.GetRequiredService<LuceneLexicalIndex>();
        var fromOtherScope = scopeTwo.ServiceProvider.GetRequiredService<ILexicalIndex>();

        Assert.Same(byType, byInterface);
        Assert.Same(byInterface, fromOtherScope);
    }

    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Qdrant:Endpoint"] = "http://localhost:6334",
                ["Qdrant:Host"] = "http://localhost:6334",
                ["Retrieval:LuceneIndexPath"] =
                    Path.Combine(Path.GetTempPath(), "hrms-lucene-di", Guid.NewGuid().ToString("N"))
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.None));

        // Prerequisites the web host supplies before AddRag runs: IConfiguration comes
        // from the host builder, the blob service from AddHrmsStorageServices, and the
        // repo from Program.cs. Stubbing them keeps this a test of AddRag itself.
        services.AddSingleton<IConfiguration>(configuration);
        services.AddScoped<IDocumentBlobService, StubDocumentBlobService>();
        services.AddScoped<Repo.Document.IDocumentRepo, StubDocumentRepo>();
        services.AddScoped<MCC.Foundation.Chunker.Abstractions.IDocumentChunkerService, StubChunkerService>();

        services.AddRag(configuration);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private sealed class StubChunkerService : MCC.Foundation.Chunker.Abstractions.IDocumentChunkerService
    {
        public Task<IReadOnlyList<MCC.Foundation.Chunker.Models.DocumentChunk>> ChunkDocumentAsync(
            MCC.Foundation.Chunker.Models.DocumentChunkRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MCC.Foundation.Chunker.Models.DocumentChunk>>([]);
    }

    private sealed class StubDocumentBlobService : IDocumentBlobService
    {
        public Task<string> UploadAsync(
            string category, string fileName, byte[] fileData,
            CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);

        public Task<byte[]> DownloadAsync(
            string documentPath, CancellationToken cancellationToken = default) => Task.FromResult<byte[]>([]);

        public Task DeleteAsync(
            string documentPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubDocumentRepo : Repo.Document.IDocumentRepo
    {
        private static Task<MSSQLResponse?> None => Task.FromResult<MSSQLResponse?>(null);

        public Task<MSSQLResponse?> GetStatisticsAsync(CancellationToken cancellationToken = default) => None;

        public Task<MSSQLResponse?> GetListAsync(
            string? category, string? searchText, int pageNumber, int pageSize,
            CancellationToken cancellationToken = default) => None;

        public Task<MSSQLResponse?> InsertAsync(
            string? category, string? name, string? path, string? createdBy,
            CancellationToken cancellationToken = default) => None;

        public Task<MSSQLResponse?> UpdateActiveAsync(
            long documentId, string active, CancellationToken cancellationToken = default) => None;

        public Task<MSSQLResponse?> DeleteAsync(
            long documentId, CancellationToken cancellationToken = default) => None;

        public Task<MSSQLResponse?> GetByIdAsync(
            long documentId, CancellationToken cancellationToken = default) => None;

        public Task<MSSQLResponse?> GetBySimilarityAsync(
            string searchText, int minScore, int topCount, CancellationToken cancellationToken = default) => None;

        public Task<MSSQLResponse?> UpdateIngestionAsync(
            long documentId, string ingestionStatus, DateTime? ingestedAt, string? ingestionError,
            int? chunkCount, CancellationToken cancellationToken = default) => None;
    }
}
