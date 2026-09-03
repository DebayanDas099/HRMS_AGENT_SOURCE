using HRMS_CHATBOT_SOURCE.Agent.Notifications;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using MCC.Foundation.CosmosHelper.CosmosHelper;
using MCC.Foundation.CosmosHelper.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class CosmosAdminNotificationStoreTests
{
    [Fact]
    public async Task GetUnreadCountAsync_NoNotifications_ReturnsZero()
    {
        var (store, _) = Build();

        Assert.Equal(0, await store.GetUnreadCountAsync());
    }

    [Fact]
    public async Task AddAsync_IncrementsTheUnreadCount()
    {
        var (store, _) = Build();

        await store.AddAsync(applicationReference: null, "9999999999", "Alice");
        await store.AddAsync(applicationReference: null, "8888888888", "Bob");

        Assert.Equal(2, await store.GetUnreadCountAsync());
    }

    [Fact]
    public async Task MarkAllReadAsync_ClearsTheUnreadCount()
    {
        var (store, _) = Build();
        await store.AddAsync(applicationReference: null, "9999999999", "Alice");

        await store.MarkAllReadAsync();

        Assert.Equal(0, await store.GetUnreadCountAsync());
    }

    [Fact]
    public async Task AddAsync_SetsATtlMatchingConfiguredDays()
    {
        var (store, cosmos) = Build(ttlDays: 90);

        await store.AddAsync(applicationReference: null, "9999999999", "Alice");

        var written = Assert.Single(cosmos.WrittenDocuments.Values);
        Assert.Equal((int)TimeSpan.FromDays(90).TotalSeconds, written.Ttl);
    }

    private static (CosmosAdminNotificationStore Store, FakeCosmosService Cosmos) Build(int ttlDays = 90)
    {
        var cosmos = new FakeCosmosService();
        var services = new ServiceCollection().AddScoped<ICosmosService>(_ => cosmos).BuildServiceProvider();

        var store = new CosmosAdminNotificationStore(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new CosmosSettings()),
            Options.Create(new NotificationSettings { AdminTtlDays = ttlDays }),
            NullLogger<CosmosAdminNotificationStore>.Instance);

        return (store, cosmos);
    }

    private sealed class FakeCosmosService : ICosmosService
    {
        private readonly Dictionary<string, AdminNotificationDocument> _documents = [];

        public Dictionary<string, AdminNotificationDocument> WrittenDocuments => _documents;

        public Task<List<CosmosResponseModel<T>>> UpsertItemsAsync<T>(CosmosUpsertRequestModel<T> request)
            where T : class
        {
            foreach (var wrapper in request.Items ?? [])
            {
                if (wrapper.Item is AdminNotificationDocument document)
                {
                    _documents[document.Id] = document;
                }
            }

            return Task.FromResult(new List<CosmosResponseModel<T>>());
        }

        public Task<IEnumerable<T>> GetItems<T>(CosmosQueryRequestModel request)
        {
            return Task.FromResult((IEnumerable<T>)_documents.Values.Cast<T>().ToList());
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
