using System.Text.Json;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using MCC.Foundation.CosmosHelper.CosmosHelper;
using MCC.Foundation.CosmosHelper.Models;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Agent.Checkpointing;

/// <summary>
/// Cosmos-backed MAF checkpoint store, matching the mandated Cosmos DB layout:
/// container "checkpoints", partition key "/threadId" (the conversation id), 7-day
/// TTL. Plugs in via CheckpointManager.CreateJson(this) in place of
/// CheckpointManager.CreateInMemory() - same ICheckpointStore&lt;JsonElement&gt; seam,
/// no call site elsewhere changes.
/// <para>
/// Resolves ICosmosService through a fresh DI scope per call rather than injecting
/// it directly: this store is held by a singleton CheckpointManager, and neither the
/// registered lifetime of ICosmosService nor MCC.Foundation.CosmosHelper's source is
/// something this codebase controls, so capturing it directly risks a captive
/// dependency. Same defensive pattern as PolicyKnowledgeTools and
/// LeaveApplicationTools.
/// </para>
/// </summary>
public sealed class CosmosCheckpointStore : ICheckpointStore<JsonElement>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CosmosSettings _settings;
    private readonly ILogger<CosmosCheckpointStore> _logger;

    public CosmosCheckpointStore(
        IServiceScopeFactory scopeFactory,
        IOptions<CosmosSettings> settings,
        ILogger<CosmosCheckpointStore> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async ValueTask<CheckpointInfo> CreateCheckpointAsync(
        string sessionId,
        JsonElement value,
        CheckpointInfo? parent = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var document = new CosmosCheckpointDocument
        {
            Id = Guid.NewGuid().ToString("N"),
            ThreadId = sessionId,
            ParentCheckpointId = parent?.CheckpointId,
            Payload = value.GetRawText(),
            CommittedUtc = DateTimeOffset.UtcNow,
            Ttl = checked((int)TimeSpan.FromDays(Math.Max(1, _settings.CheckpointTtlDays)).TotalSeconds)
        };

        await cosmos.UpsertItemsAsync(new CosmosUpsertRequestModel<CosmosCheckpointDocument>
        {
            DatabaseName = _settings.DatabaseName,
            ContainerName = _settings.CheckpointContainerName,
            Items =
            [
                new CosmosUpsertRequestModelWrapper<CosmosCheckpointDocument>
                {
                    Item = document,
                    PartitionKey = sessionId
                }
            ]
        }).ConfigureAwait(false);

        return new CheckpointInfo(sessionId, document.Id);
    }

    public async ValueTask<JsonElement> RetrieveCheckpointAsync(string sessionId, CheckpointInfo key)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var results = await cosmos.GetItems<CosmosCheckpointDocument>(new CosmosQueryRequestModel
        {
            DatabaseName = _settings.DatabaseName,
            ContainerName = _settings.CheckpointContainerName,
            PartitionKey = sessionId,
            QueryString = "SELECT * FROM c WHERE c.id = @id",
            Parameters = new Dictionary<string, string> { ["@id"] = key.CheckpointId }
        }).ConfigureAwait(false);

        var document = results.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Checkpoint {key.CheckpointId} was not found for session {sessionId}.");

        // JsonDocument owns the buffer the parsed element points into; Clone() detaches
        // the element so it stays valid after the document (and this using block) is gone.
        using var parsed = JsonDocument.Parse(document.Payload);
        return parsed.RootElement.Clone();
    }

    public async ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(string sessionId, CheckpointInfo? withParent = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var queryString = withParent is null
            ? "SELECT * FROM c ORDER BY c.committedUtc ASC"
            : "SELECT * FROM c WHERE c.parentCheckpointId = @parentId ORDER BY c.committedUtc ASC";

        var parameters = withParent is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["@parentId"] = withParent.CheckpointId };

        var results = await cosmos.GetItems<CosmosCheckpointDocument>(new CosmosQueryRequestModel
        {
            DatabaseName = _settings.DatabaseName,
            ContainerName = _settings.CheckpointContainerName,
            PartitionKey = sessionId,
            QueryString = queryString,
            Parameters = parameters
        }).ConfigureAwait(false);

        var index = results.Select(document => new CheckpointInfo(sessionId, document.Id)).ToList();

        _logger.LogDebug(
            "Retrieved {CheckpointCount} checkpoints for session {SessionId}{ParentClause}.",
            index.Count,
            sessionId,
            withParent is null ? string.Empty : $" under parent {withParent.CheckpointId}");

        return index;
    }
}
