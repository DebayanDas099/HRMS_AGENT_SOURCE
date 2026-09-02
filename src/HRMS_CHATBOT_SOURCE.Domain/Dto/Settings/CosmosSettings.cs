using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class CosmosSettings
{
    public const string SectionName = "Cosmos";

    [JsonProperty("database_name")]
    public string DatabaseName { get; set; } = "HRMS_AGENT_DB";

    /// <summary>
    /// Per the mandated Cosmos DB layout: container "checkpoints", partition key
    /// "/threadId", 7-day TTL. The container itself (partition key, TTL enabled)
    /// must be provisioned in Azure before this is usable - nothing in this
    /// codebase creates Cosmos containers.
    /// </summary>
    [JsonProperty("checkpoint_container_name")]
    public string CheckpointContainerName { get; set; } = "checkpoints";

    [JsonProperty("checkpoint_ttl_days")]
    public int CheckpointTtlDays { get; set; } = 7;
}
