using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class NotificationSettings
{
    public const string SectionName = "Notification";

    /// <summary>
    /// Same Cosmos account/database as checkpoints and conversation history
    /// (<see cref="CosmosSettings.DatabaseName"/>). Partition key "/mobile", TTL enabled.
    /// The container itself must be provisioned in Azure before this is usable -
    /// nothing in this codebase creates Cosmos containers.
    /// </summary>
    [JsonProperty("leave_status_container_name")]
    public string LeaveStatusContainerName { get; set; } = "leave-notifications";

    [JsonProperty("leave_status_ttl_days")]
    public int LeaveStatusTtlDays { get; set; } = 90;

    /// <summary>
    /// Admin-facing "a leave application needs action" notifications. Partition key
    /// is a constant - the Admin area is a single generic role today, not per-manager
    /// (no distinct approver identities exist to key on).
    /// </summary>
    [JsonProperty("admin_container_name")]
    public string AdminContainerName { get; set; } = "admin-notifications";

    [JsonProperty("admin_ttl_days")]
    public int AdminTtlDays { get; set; } = 90;
}
