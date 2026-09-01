using System.Text.Json;
using HRMS_CHATBOT_SOURCE.Agent.Checkpointing;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using MCC.Foundation.CosmosHelper.CosmosHelper;
using MCC.Foundation.CosmosHelper.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class CosmosCheckpointStoreTests
{
    [Fact]
    public async Task CreateCheckpointAsync_WritesAPartitionedDocumentAndReturnsItsInfo()
    {
        var (store, cosmos) = Build();
        var payload = Parse("""{"step":1}""");

        var info = await store.CreateCheckpointAsync("thread-1", payload);

        Assert.Equal("thread-1", info.SessionId);
        var written = Assert.Single(cosmos.WrittenDocuments);
        Assert.Equal("thread-1", written.ThreadId);
        Assert.Null(written.ParentCheckpointId);
        Assert.Equal(info.CheckpointId, written.Id);
    }

    [Fact]
    public async Task CreateCheckpointAsync_LinksToTheGivenParent()
    {
        var (store, cosmos) = Build();
        var first = await store.CreateCheckpointAsync("thread-1", Parse("""{"step":1}"""));

        var second = await store.CreateCheckpointAsync("thread-1", Parse("""{"step":2}"""), first);

        var child = cosmos.WrittenDocuments.Single(d => d.Id == second.CheckpointId);
        Assert.Equal(first.CheckpointId, child.ParentCheckpointId);
    }

    [Fact]
    public async Task CreateCheckpointAsync_SetsATtlMatchingConfiguredDays()
    {
        var (store, cosmos) = Build(ttlDays: 7);

        var info = await store.CreateCheckpointAsync("thread-1", Parse("{}"));

        var written = cosmos.WrittenDocuments.Single(d => d.Id == info.CheckpointId);
        Assert.Equal((int)TimeSpan.FromDays(7).TotalSeconds, written.Ttl);
    }

    [Fact]
    public async Task RetrieveCheckpointAsync_RoundTripsThePayload()
    {
        var (store, _) = Build();
        var original = Parse("""{"step":1,"nested":{"ok":true}}""");
        var info = await store.CreateCheckpointAsync("thread-1", original);

        // The retrieved element must still be usable after the method returns - this is
        // what proves JsonDocument.Parse(...).RootElement.Clone() actually detached it,
        // rather than handing back an element tied to a disposed document.
        var retrieved = await store.RetrieveCheckpointAsync("thread-1", info);

        Assert.Equal(1, retrieved.GetProperty("step").GetInt32());
        Assert.True(retrieved.GetProperty("nested").GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task RetrieveCheckpointAsync_UnknownCheckpoint_Throws()
    {
        var (store, _) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.RetrieveCheckpointAsync("thread-1", new Microsoft.Agents.AI.Workflows.CheckpointInfo("thread-1", "missing")).AsTask());
    }

    [Fact]
    public async Task RetrieveIndexAsync_ReturnsCheckpointsOldestFirst()
    {
        // This ordering is the load-bearing contract: CheckpointManager.GetLatestCheckpointAsync
        // relies on the store returning oldest-to-newest and takes the last element as
        // "latest". Returning newest-first (or unordered) would make "latest" silently wrong.
        var (store, cosmos) = Build();
        var first = await store.CreateCheckpointAsync("thread-1", Parse("""{"n":1}"""));
        cosmos.Advance();
        var second = await store.CreateCheckpointAsync("thread-1", Parse("""{"n":2}"""));
        cosmos.Advance();
        var third = await store.CreateCheckpointAsync("thread-1", Parse("""{"n":3}"""));

        var index = (await store.RetrieveIndexAsync("thread-1")).ToList();

        Assert.Equal([first.CheckpointId, second.CheckpointId, third.CheckpointId], index.Select(c => c.CheckpointId));
    }

    [Fact]
    public async Task RetrieveIndexAsync_WithParent_ReturnsOnlyItsChildren()
    {
        var (store, cosmos) = Build();
        var root = await store.CreateCheckpointAsync("thread-1", Parse("{}"));
        cosmos.Advance();
        var childOfRoot = await store.CreateCheckpointAsync("thread-1", Parse("{}"), root);
        cosmos.Advance();
        var unrelated = await store.CreateCheckpointAsync("thread-1", Parse("{}"));

        var children = (await store.RetrieveIndexAsync("thread-1", root)).ToList();

        Assert.Single(children);
        Assert.Equal(childOfRoot.CheckpointId, children[0].CheckpointId);
        Assert.DoesNotContain(children, c => c.CheckpointId == unrelated.CheckpointId);
    }

    [Fact]
    public async Task RetrieveIndexAsync_IsScopedToItsSession()
    {
        var (store, cosmos) = Build();
        await store.CreateCheckpointAsync("thread-1", Parse("{}"));
        cosmos.Advance();
        await store.CreateCheckpointAsync("thread-2", Parse("{}"));

        var threadOneIndex = await store.RetrieveIndexAsync("thread-1");

        Assert.Single(threadOneIndex);
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static (CosmosCheckpointStore Store, FakeCosmosService Cosmos) Build(int ttlDays = 7)
    {
        var cosmos = new FakeCosmosService();
        var services = new ServiceCollection().AddScoped<ICosmosService>(_ => cosmos).BuildServiceProvider();

        var store = new CosmosCheckpointStore(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CosmosSettings { CheckpointTtlDays = ttlDays }),
            NullLogger<CosmosCheckpointStore>.Instance);

        return (store, cosmos);
    }

    /// <summary>
    /// Simulates only the request shapes CosmosCheckpointStore actually emits - a
    /// partition-scoped point read by id, an unfiltered index query, and a
    /// parent-filtered index query, both ordered by committedUtc. Not a general
    /// Cosmos emulator; the two sides of this fake are written by the same author
    /// against the same three query strings deliberately, mirroring the stub pattern
    /// already used by RagServiceRegistrationTests for IDocumentRepo/IDocumentChunkerService.
    /// </summary>
    private sealed class FakeCosmosService : ICosmosService
    {
        private readonly List<CosmosCheckpointDocument> _documents = [];
        private int _clock;

        public List<CosmosCheckpointDocument> WrittenDocuments => _documents;

        /// <summary>Forces a strictly increasing CommittedUtc between writes in the same test tick.</summary>
        public void Advance() => _clock++;

        public Task<List<CosmosResponseModel<T>>> UpsertItemsAsync<T>(CosmosUpsertRequestModel<T> request)
            where T : class
        {
            foreach (var wrapper in request.Items ?? [])
            {
                if (wrapper.Item is CosmosCheckpointDocument document)
                {
                    document.CommittedUtc = document.CommittedUtc.AddTicks(_clock);
                    _documents.Add(document);
                }
            }

            return Task.FromResult(new List<CosmosResponseModel<T>>());
        }

        public Task<IEnumerable<T>> GetItems<T>(CosmosQueryRequestModel request)
        {
            var queryString = request.QueryString ?? string.Empty;
            var parameters = request.Parameters ?? new Dictionary<string, string>();

            IEnumerable<CosmosCheckpointDocument> matches = _documents
                .Where(d => d.ThreadId == request.PartitionKey);

            if (queryString.Contains("c.id = @id"))
            {
                var id = parameters["@id"];
                matches = matches.Where(d => d.Id == id);
            }
            else if (queryString.Contains("c.parentCheckpointId = @parentId"))
            {
                var parentId = parameters["@parentId"];
                matches = matches.Where(d => d.ParentCheckpointId == parentId);
            }

            if (queryString.Contains("ORDER BY c.committedUtc ASC"))
            {
                matches = matches.OrderBy(d => d.CommittedUtc);
            }

            return Task.FromResult((IEnumerable<T>)matches.Cast<T>().ToList());
        }

        public Task<List<CosmosResponseModel<T>>> InsertItemsAsync<T>(CosmosUpsertRequestModel<T> request)
            where T : class
            => throw new NotSupportedException();

        public Task<object> ExecStoreProcedure(CosmosSPRequestModel request) => throw new NotSupportedException();

        public Task<object> ExecStoreProcedure(CosmosSPRequestV2Model request) => throw new NotSupportedException();

        public Task<bool> CheckValueInProperty(CosmosExistenceModel request) => throw new NotSupportedException();

        public Task<List<CosmosUpdateResponseModel<T>>> UpdateItemsAsync<T>(CosmosUpdateRequestModel<T> request)
            where T : class
            => throw new NotSupportedException();

        public Task<List<CosmosResponseModel<T>>> DeleteDocuments<T>(CosmosDeleteRequestModel request)
            where T : class
            => throw new NotSupportedException();

        public Task<List<CosmosResponseModel<T>>> DeleteDocumentsByIds<T>(CosmosDeleteRequestByIdsModel deleteRequest)
            where T : class
            => throw new NotSupportedException();

        public Task<List<CosmosResponseModel<T>>> DeleteDocumentsByIds<T>(CosmosDeleteRequestByStringIdsModel deleteRequest)
            where T : class
            => throw new NotSupportedException();

        public Task<IEnumerable<T>> VectorSearchAsync<T>(CosmosVectorSearchRequestModel request)
            => throw new NotSupportedException();

        public Task<List<CosmosResponseModel<T>>> VectorSaveAsync<T>(CosmosVectorSaveRequestModel<T> request)
            where T : class
            => throw new NotSupportedException();

        public Task<CosmosSemanticRerankResponseModel> SemanticRerankAsync(
            CosmosSemanticRerankRequestModel request, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<CosmosLuceneRerankResponseModel<T>> LuceneRerankAsync<T>(
            CosmosLuceneRerankRequestModel request, CancellationToken cancellationToken)
            where T : class
            => throw new NotSupportedException();
    }
}
