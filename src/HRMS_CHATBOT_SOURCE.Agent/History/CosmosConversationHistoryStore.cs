using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using MCC.Foundation.CosmosHelper.CosmosHelper;
using MCC.Foundation.CosmosHelper.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Agent.History;

/// <summary>
/// Cosmos-backed conversation history, in the same account/database as the checkpoint
/// store but its own container: partition key "/conversationId". Resolves
/// ICosmosService through a fresh DI scope per call rather than injecting it directly -
/// this store is held by a singleton, same defensive pattern as CosmosCheckpointStore,
/// PolicyKnowledgeTools and LeaveApplicationTools.
/// </summary>
public sealed class CosmosConversationHistoryStore : IConversationHistoryStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CosmosSettings _cosmosSettings;
    private readonly ConversationHistorySettings _historySettings;
    private readonly ILogger<CosmosConversationHistoryStore> _logger;

    public CosmosConversationHistoryStore(
        IServiceScopeFactory scopeFactory,
        IOptions<CosmosSettings> cosmosSettings,
        IOptions<ConversationHistorySettings> historySettings,
        ILogger<CosmosConversationHistoryStore> logger)
    {
        _scopeFactory = scopeFactory;
        _cosmosSettings = cosmosSettings.Value;
        _historySettings = historySettings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var results = await cosmos.GetItems<ConversationHistoryDocument>(new CosmosQueryRequestModel
        {
            DatabaseName = _cosmosSettings.DatabaseName,
            ContainerName = _historySettings.ContainerName,
            PartitionKey = conversationId,
            QueryString = "SELECT * FROM c WHERE c.id = @id",
            Parameters = new Dictionary<string, string> { ["@id"] = conversationId }
        }).ConfigureAwait(false);

        var document = results.FirstOrDefault();
        if (document is null)
        {
            return [];
        }

        return document.Messages
            .Select(m => new ChatMessage(new ChatRole(m.Role), m.Text))
            .ToList();
    }

    public async Task SaveHistoryAsync(string conversationId, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<ICosmosService>();

        var document = new ConversationHistoryDocument
        {
            Id = conversationId,
            ConversationId = conversationId,
            Messages = messages
                .Select(m => new StoredChatMessage { Role = m.Role.Value, Text = m.Text })
                .ToList(),
            UpdatedUtc = DateTimeOffset.UtcNow,
            Ttl = checked((int)TimeSpan.FromDays(Math.Max(1, _historySettings.TtlDays)).TotalSeconds)
        };

        await cosmos.UpsertItemsAsync(new CosmosUpsertRequestModel<ConversationHistoryDocument>
        {
            DatabaseName = _cosmosSettings.DatabaseName,
            ContainerName = _historySettings.ContainerName,
            Items =
            [
                new CosmosUpsertRequestModelWrapper<ConversationHistoryDocument>
                {
                    Item = document,
                    PartitionKey = conversationId
                }
            ]
        }).ConfigureAwait(false);

        _logger.LogDebug(
            "Saved {MessageCount} messages for conversation {ConversationId}.",
            document.Messages.Count,
            conversationId);
    }
}
