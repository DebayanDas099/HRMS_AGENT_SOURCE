using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class ConversationHistorySettings
{
    public const string SectionName = "ConversationHistory";

    /// <summary>
    /// Same Cosmos account/database as checkpoints (<see cref="CosmosSettings.DatabaseName"/>),
    /// a separate container: partition key "/conversationId", TTL enabled.
    /// The container itself must be provisioned in Azure before this is usable -
    /// nothing in this codebase creates Cosmos containers.
    /// </summary>
    [JsonProperty("container_name")]
    public string ContainerName { get; set; } = "conversation-history";

    [JsonProperty("ttl_days")]
    public int TtlDays { get; set; } = 30;
}
