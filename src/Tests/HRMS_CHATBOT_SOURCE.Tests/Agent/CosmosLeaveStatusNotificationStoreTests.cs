using HRMS_CHATBOT_SOURCE.Agent.Notifications;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using MCC.Foundation.CosmosHelper.CosmosHelper;
using MCC.Foundation.CosmosHelper.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class CosmosLeaveStatusNotificationStoreTests
{
    [Fact]
    public async Task GetPendingAsync_UnknownMobile_ReturnsEmpty()
    {
        var (store, _) = Build();

        var pending = await store.GetPendingAsync("9999999999");

        Assert.Empty(pending);
    }

    [Fact]
    public async Task AddAsync_ThenGetPendingAsync_RoundTrips()
    {
        var (store, _) = Build();

        await store.AddAsync("9999999999", "ref-1", "Approved", "Enjoy your trip", default);
        var pending = await store.GetPendingAsync("9999999999");

        var notification = Assert.Single(pending);
        Assert.Equal("ref-1", notification.ApplicationReference);
        Assert.Equal("Approved", notification.NewStatus);
        Assert.Equal("Enjoy your trip", notification.Note);
    }

    [Fact]
    public async Task MarkDeliveredAsync_RemovesTheEntryFromPending()
    {
        var (store, _) = Build();
        await store.AddAsync("9999999999", "ref-1", "Approved", null, default);
        var pending = await store.GetPendingAsync("9999999999");

        await store.MarkDeliveredAsync("9999999999", pending.Select(p => p.Id).ToList());
        var afterDelivery = await store.GetPendingAsync("9999999999");

        Assert.Empty(afterDelivery);
    }

    [Fact]
    public async Task AddAsync_SetsATtlMatchingConfiguredDays()
    {
        var (store, cosmos) = Build(ttlDays: 90);

        await store.AddAsync("9999999999", "ref-1", "Approved", null, default);

        var written = Assert.Single(cosmos.WrittenDocuments.Values);
        Assert.Equal((int)TimeSpan.FromDays(90).TotalSeconds, written.Ttl);
    }

    [Fact]
    public async Task GetPendingAsync_IsScopedToItsMobile()
    {
        var (store, _) = Build();
        await store.AddAsync("9999999999", "ref-1", "Approved", null, default);
        await store.AddAsync("8888888888", "ref-2", "Rejected", null, default);

        var pending = await store.GetPendingAsync("9999999999");

        Assert.Single(pending);
    }

    private static (CosmosLeaveStatusNotificationStore Store, FakeCosmosService Cosmos) Build(int ttlDays = 90)
    {
        var cosmos = new FakeCosmosService();
        var services = new ServiceCollection().AddScoped<ICosmosService>(_ => cosmos).BuildServiceProvider();

        var store = new CosmosLeaveStatusNotificationStore(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CosmosSettings()),
            Options.Create(new NotificationSettings { LeaveStatusTtlDays = ttlDays }),
            NullLogger<CosmosLeaveStatusNotificationStore>.Instance);

        return (store, cosmos);
    }

    /// <summary>
    /// Simulates only the request shapes CosmosLeaveStatusNotificationStore actually
    /// emits - a partition-scoped read by mobile and an upsert. Not a general Cosmos
    /// emulator; mirrors the fake already used by CosmosCheckpointStoreTests.
    /// </summary>
    private sealed class FakeCosmosService : ICosmosService
    {
        private readonly Dictionary<string, LeaveStatusNotificationDocument> _documents = [];

        public Dictionary<string, LeaveStatusNotificationDocument> WrittenDocuments => _documents;

        public Task<List<CosmosResponseModel<T>>> UpsertItemsAsync<T>(CosmosUpsertRequestModel<T> request)
            where T : class
        {
            foreach (var wrapper in request.Items ?? [])
            {
                if (wrapper.Item is LeaveStatusNotificationDocument document)
                {
                    _documents[document.Id] = document;
                }
            }

            return Task.FromResult(new List<CosmosResponseModel<T>>());
        }

        public Task<IEnumerable<T>> GetItems<T>(CosmosQueryRequestModel request)
        {
            var matches = _documents.Values.Where(d => d.Mobile == request.PartitionKey);
            return Task.FromResult((IEnumerable<T>)matches.Cast<T>().ToList());
        }

        public Task<List<CosmosResponseModel<T>>> InsertItemsAsync<T>(CosmosUpsertRequestModel<T> request) where T : class => throw new NotSupportedException();
        public Task<object> ExecStoreProcedure(CosmosSPRequestModel request) => throw new NotSupportedException();
        public Task<object> ExecStoreProcedure(CosmosSPRequestV2Model request) => throw new NotSupportedException();
        public Task<bool> CheckValueInProperty(CosmosExistenceModel request) => throw new NotSupportedException();
        public Task<List<CosmosUpdateResponseModel<T>>> UpdateItemsAsync<T>(CosmosUpdateRequestModel<T> request) where T : class => throw new NotSupportedException();
        public Task<List<CosmosResponseModel<T>>> DeleteDocuments<T>(CosmosDeleteRequestModel request) where T : class => throw new NotSupportedException();
        public Task<List<CosmosResponseModel<T>>> DeleteDocumentsByIds<T>(CosmosDeleteRequestByIdsModel deleteRequest) where T : class => throw new NotSupportedException();
        public Task<List<CosmosResponseModel<T>>> DeleteDocumentsByIds<T>(CosmosDeleteRequestByStringIdsModel deleteRequest) where T : class => throw new NotSupportedException();
        public Task<IEnumerable<T>> VectorSearchAsync<T>(CosmosVectorSearchRequestModel request) => throw new NotSupportedException();
        public Task<List<CosmosResponseModel<T>>> VectorSaveAsync<T>(CosmosVectorSaveRequestModel<T> request) where T : class => throw new NotSupportedException();
        public Task<CosmosSemanticRerankResponseModel> SemanticRerankAsync(CosmosSemanticRerankRequestModel request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<CosmosLuceneRerankResponseModel<T>> LuceneRerankAsync<T>(CosmosLuceneRerankRequestModel request, CancellationToken cancellationToken) where T : class => throw new NotSupportedException();
    }
}
