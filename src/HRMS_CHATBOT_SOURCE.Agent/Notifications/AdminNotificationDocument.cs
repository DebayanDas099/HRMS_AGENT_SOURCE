using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

/// <summary>
/// The Cosmos envelope an admin-facing "needs action" notification is stored as -
/// today, exclusively "a leave application was submitted". Partition key is a
/// constant ("admin"): the Admin area is a single generic role, not per-manager.
/// </summary>
internal sealed class AdminNotificationDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Constant "admin". Partition key path: /audience.</summary>
    [JsonProperty("audience")]
    public string Audience { get; set; } = "admin";

    [JsonProperty("type")]
    public string Type { get; set; } = "LeaveApplied";

    /// <summary>
    /// Null at the moment of applying - the apply stored procedure returns only a
    /// success/failure message, no reference. Set once the Approvals grid's own
    /// "pending applications" query establishes one; this notification's role is
    /// only to wake the bell, not to deep-link a specific row.
    /// </summary>
    [JsonProperty("applicationReference")]
    public string? ApplicationReference { get; set; }

    [JsonProperty("employeeMobile")]
    public string EmployeeMobile { get; set; } = string.Empty;

    [JsonProperty("employeeName")]
    public string? EmployeeName { get; set; }

    [JsonProperty("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Null while unread. Set when the Approvals grid is opened.</summary>
    [JsonProperty("readUtc")]
    public DateTimeOffset? ReadUtc { get; set; }

    [JsonProperty("ttl")]
    public int Ttl { get; set; }
}
