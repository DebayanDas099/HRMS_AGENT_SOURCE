using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using MCC.Foundation.CosmosHelper.CosmosHelper;
using MCC.Foundation.CosmosHelper.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

/// <summary>
/// Cosmos-backed employee notification store, in the same account/database as the
/// checkpoint and conversation-history stores but its own container: partition key
/// "/mobile". Resolves ICosmosService through a fresh DI scope per call, same
/// defensive pattern as CosmosCheckpointStore and CosmosConversationHistoryStore -
/// this store is held by a singleton, and it is also the target of fire-and-forget
/// calls from request-scoped callers, so it must never reuse a caller's scope.
/// </summary>
public sealed class CosmosLeaveStatusNotificationStore : ILeaveStatusNotificationStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CosmosSettings _cosmosSettings;
    private readonly NotificationSettings _notificationSettings;
    private readonly ILogger<CosmosLeaveStatusNotificationStore> _logger;

    public CosmosLeaveStatusNotificationStore(
        IServiceScopeFactory scopeFactory,
        IOptions<CosmosSettings> cosmosSettings,
        IOptions<NotificationSettings> notificationSettings,
        ILogger<CosmosLeaveStatusNotificationStore> logger)
    {
        _scopeFactory = scopeFactory;
        _cosmosSettings = cosmosSettings.Value;
        _notificationSettings = notificationSettings.Value;
        _logger = logger;
    }

    public async Task AddAsync(
        string mobile,
        string applicationReference,
        string newStatus,
        string? note,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var document = new LeaveStatusNotificationDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            Mobile = mobile,
            ApplicationReference = applicationReference,
            NewStatus = newStatus,
            Note = note,
            CreatedUtc = DateTimeOffset.UtcNow,
            DeliveredUtc = null,
            Ttl = checked((int)TimeSpan.FromDays(Math.Max(1, _notificationSettings.LeaveStatusTtlDays)).TotalSeconds)
        };

        await cosmos.UpsertItemsAsync(new CosmosUpsertRequestModel<LeaveStatusNotificationDocument>
        {
            DatabaseName = _cosmosSettings.DatabaseName,
            ContainerName = _notificationSettings.LeaveStatusContainerName,
            Items =
            [
                new CosmosUpsertRequestModelWrapper<LeaveStatusNotificationDocument>
                {
                    Item = document,
                    PartitionKey = mobile
                }
            ]
        }).ConfigureAwait(false);

        _logger.LogInformation(
            "Recorded leave status notification for {Mobile}: {Reference} -> {Status}.",
            mobile,
            applicationReference,
            newStatus);
    }

    public async Task<IReadOnlyList<PendingLeaveStatusNotification>> GetPendingAsync(
        string mobile,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var documents = await QueryByMobileAsync(cosmos, mobile).ConfigureAwait(false);

        return documents
            .Where(d => d.DeliveredUtc is null)
            .OrderBy(d => d.CreatedUtc)
            .Select(d => new PendingLeaveStatusNotification(d.Id, d.ApplicationReference, d.NewStatus, d.Note, d.CreatedUtc))
            .ToList();
    }

    public async Task MarkDeliveredAsync(
        string mobile,
        IReadOnlyCollection<string> notificationIds,
        CancellationToken cancellationToken = default)
    {
        if (notificationIds.Count == 0)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var documents = await QueryByMobileAsync(cosmos, mobile).ConfigureAwait(false);
        var toDeliver = documents.Where(d => notificationIds.Contains(d.Id)).ToList();

        if (toDeliver.Count == 0)
        {
            return;
        }

        var deliveredUtc = DateTimeOffset.UtcNow;
        foreach (var document in toDeliver)
        {
            document.DeliveredUtc = deliveredUtc;
        }

        await cosmos.UpsertItemsAsync(new CosmosUpsertRequestModel<LeaveStatusNotificationDocument>
        {
            DatabaseName = _cosmosSettings.DatabaseName,
            ContainerName = _notificationSettings.LeaveStatusContainerName,
            Items = toDeliver
                .Select(document => new CosmosUpsertRequestModelWrapper<LeaveStatusNotificationDocument>
                {
                    Item = document,
                    PartitionKey = mobile
                })
                .ToList()
        }).ConfigureAwait(false);
    }

    private async Task<List<LeaveStatusNotificationDocument>> QueryByMobileAsync(ICosmosService cosmos, string mobile)
    {
        var results = await cosmos.GetItems<LeaveStatusNotificationDocument>(new CosmosQueryRequestModel
        {
            DatabaseName = _cosmosSettings.DatabaseName,
            ContainerName = _notificationSettings.LeaveStatusContainerName,
            PartitionKey = mobile,
            QueryString = "SELECT * FROM c WHERE c.mobile = @mobile",
            Parameters = new Dictionary<string, string> { ["@mobile"] = mobile }
        }).ConfigureAwait(false);

        return results.ToList();
    }
}
