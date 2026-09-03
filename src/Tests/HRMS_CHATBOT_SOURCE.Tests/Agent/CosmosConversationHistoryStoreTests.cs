using HRMS_CHATBOT_SOURCE.Agent.History;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using MCC.Foundation.CosmosHelper.CosmosHelper;
using MCC.Foundation.CosmosHelper.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class CosmosConversationHistoryStoreTests
{
    [Fact]
    public async Task GetHistoryAsync_UnknownConversation_ReturnsEmpty()
    {
        var (store, _) = Build();

        var history = await store.GetHistoryAsync("conversation-1");

        Assert.Empty(history);
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsRoleAndText()
    {
        var (store, _) = Build();
        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, "hello"),
            new(ChatRole.Assistant, "hi there")
        };

        await store.SaveHistoryAsync("conversation-1", messages);
        var retrieved = await store.GetHistoryAsync("conversation-1");

        Assert.Equal(2, retrieved.Count);
        Assert.Equal(ChatRole.User, retrieved[0].Role);
        Assert.Equal("hello", retrieved[0].Text);
        Assert.Equal(ChatRole.Assistant, retrieved[1].Role);
        Assert.Equal("hi there", retrieved[1].Text);
    }

    [Fact]
    public async Task SaveHistoryAsync_UpsertsAsASingleDocumentPerConversation()
    {
        var (store, cosmos) = Build();

        await store.SaveHistoryAsync("conversation-1", [new ChatMessage(ChatRole.User, "first")]);
        await store.SaveHistoryAsync(
            "conversation-1",
            [new ChatMessage(ChatRole.User, "first"), new ChatMessage(ChatRole.Assistant, "second")]);

        var written = Assert.Single(cosmos.WrittenDocuments);
        Assert.Equal(2, written.Messages.Count);
    }

    [Fact]
    public async Task SaveHistoryAsync_SetsATtlMatchingConfiguredDays()
    {
        var (store, cosmos) = Build(ttlDays: 30);

        await store.SaveHistoryAsync("conversation-1", [new ChatMessage(ChatRole.User, "hi")]);

        var written = Assert.Single(cosmos.WrittenDocuments);
        Assert.Equal((int)TimeSpan.FromDays(30).TotalSeconds, written.Ttl);
    }

    [Fact]
    public async Task GetHistoryAsync_IsScopedToItsConversation()
    {
        var (store, _) = Build();
        await store.SaveHistoryAsync("conversation-1", [new ChatMessage(ChatRole.User, "a")]);
        await store.SaveHistoryAsync("conversation-2", [new ChatMessage(ChatRole.User, "b")]);

        var historyOne = await store.GetHistoryAsync("conversation-1");

        var text = Assert.Single(historyOne).Text;
        Assert.Equal("a", text);
    }

    private static (CosmosConversationHistoryStore Store, FakeCosmosService Cosmos) Build(int ttlDays = 30)
    {
        var cosmos = new FakeCosmosService();
        var services = new ServiceCollection().AddScoped<ICosmosService>(_ => cosmos).BuildServiceProvider();

        var store = new CosmosConversationHistoryStore(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CosmosSettings()),
            Options.Create(new ConversationHistorySettings { TtlDays = ttlDays }),
            NullLogger<CosmosConversationHistoryStore>.Instance);

        return (store, cosmos);
    }

    /// <summary>
    /// Simulates only the request shapes CosmosConversationHistoryStore actually emits -
    /// a partition-scoped point read by id and an upsert. Not a general Cosmos emulator;
    /// mirrors the fake already used by CosmosCheckpointStoreTests.
    /// </summary>
    private sealed class FakeCosmosService : ICosmosService
    {
        private readonly Dictionary<string, ConversationHistoryDocument> _documents = [];

        public List<ConversationHistoryDocument> WrittenDocuments => [.. _documents.Values];

        public Task<List<CosmosResponseModel<T>>> UpsertItemsAsync<T>(CosmosUpsertRequestModel<T> request)
            where T : class
        {
            foreach (var wrapper in request.Items ?? [])
            {
                if (wrapper.Item is ConversationHistoryDocument document)
                {
                    _documents[document.Id] = document;
                }
            }

            return Task.FromResult(new List<CosmosResponseModel<T>>());
        }

        public Task<IEnumerable<T>> GetItems<T>(CosmosQueryRequestModel request)
        {
            IEnumerable<ConversationHistoryDocument> matches = _documents.Values
                .Where(d => d.ConversationId == request.PartitionKey);

            if ((request.QueryString ?? string.Empty).Contains("c.id = @id"))
            {
                var id = request.Parameters?["@id"];
                matches = matches.Where(d => d.Id == id);
            }

            return Task.FromResult((IEnumerable<T>)matches.ToList());
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
