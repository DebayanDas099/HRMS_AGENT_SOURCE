using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using MCC.Foundation.CosmosHelper.CosmosHelper;
using MCC.Foundation.CosmosHelper.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

/// <summary>
/// Cosmos-backed admin notification store. Same per-call DI scoping as the other
/// stores in this folder - this store is also a fire-and-forget target from
/// request-scoped callers (LeaveApplicationTools), so it must never reuse a
/// caller's scope.
/// </summary>
public sealed class CosmosAdminNotificationStore : IAdminNotificationStore
{
    private const string Audience = "admin";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CosmosSettings _cosmosSettings;
    private readonly NotificationSettings _notificationSettings;
    private readonly ILogger<CosmosAdminNotificationStore> _logger;

    public CosmosAdminNotificationStore(
        IServiceScopeFactory scopeFactory,
        IOptions<CosmosSettings> cosmosSettings,
        IOptions<NotificationSettings> notificationSettings,
        ILogger<CosmosAdminNotificationStore> logger)
    {
        _scopeFactory = scopeFactory;
        _cosmosSettings = cosmosSettings.Value;
        _notificationSettings = notificationSettings.Value;
        _logger = logger;
    }

    public async Task AddAsync(
        string? applicationReference,
        string employeeMobile,
        string? employeeName,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var document = new AdminNotificationDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            Audience = Audience,
            Type = "LeaveApplied",
            ApplicationReference = applicationReference,
            EmployeeMobile = employeeMobile,
            EmployeeName = employeeName,
            CreatedUtc = DateTimeOffset.UtcNow,
            ReadUtc = null,
            Ttl = checked((int)TimeSpan.FromDays(Math.Max(1, _notificationSettings.AdminTtlDays)).TotalSeconds)
        };

        await cosmos.UpsertItemsAsync(new CosmosUpsertRequestModel<AdminNotificationDocument>
        {
            DatabaseName = _cosmosSettings.DatabaseName,
            ContainerName = _notificationSettings.AdminContainerName,
            Items =
            [
                new CosmosUpsertRequestModelWrapper<AdminNotificationDocument>
                {
                    Item = document,
                    PartitionKey = Audience
                }
            ]
        }).ConfigureAwait(false);

        _logger.LogInformation(
            "Recorded admin notification: application {Reference} applied by {Mobile}.",
            applicationReference,
            employeeMobile);
    }

    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var documents = await QueryUnreadAsync(cosmos).ConfigureAwait(false);
        return documents.Count;
    }

    private async Task<List<AdminNotificationDocument>> QueryUnreadAsync(ICosmosService cosmos)
    {
        // Filtered client-side rather than with a Cosmos-side null check: ReadUtc is
        // always explicitly serialized (as JSON null, not an omitted property) since
        // AddAsync sets it, so an IS_DEFINED-based WHERE clause would never match -
        // the property is defined, just null.
        var results = await QueryAllAsync(cosmos).ConfigureAwait(false);
        return results.Where(d => d.ReadUtc is null).ToList();
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var unread = await QueryUnreadAsync(cosmos).ConfigureAwait(false);
        if (unread.Count == 0)
        {
            return;
        }

        var readUtc = DateTimeOffset.UtcNow;
        foreach (var document in unread)
        {
            document.ReadUtc = readUtc;
        }

        await cosmos.UpsertItemsAsync(new CosmosUpsertRequestModel<AdminNotificationDocument>
        {
            DatabaseName = _cosmosSettings.DatabaseName,
            ContainerName = _notificationSettings.AdminContainerName,
            Items = unread
                .Select(document => new CosmosUpsertRequestModelWrapper<AdminNotificationDocument>
                {
                    Item = document,
                    PartitionKey = Audience
                })
                .ToList()
        }).ConfigureAwait(false);
    }

    private async Task<List<AdminNotificationDocument>> QueryAllAsync(ICosmosService cosmos)
    {
        var results = await cosmos.GetItems<AdminNotificationDocument>(new CosmosQueryRequestModel
        {
            DatabaseName = _cosmosSettings.DatabaseName,
            ContainerName = _notificationSettings.AdminContainerName,
            PartitionKey = Audience,
            QueryString = "SELECT * FROM c WHERE c.audience = @audience",
            Parameters = new Dictionary<string, string> { ["@audience"] = Audience }
        }).ConfigureAwait(false);

        return results.ToList();
    }
}
