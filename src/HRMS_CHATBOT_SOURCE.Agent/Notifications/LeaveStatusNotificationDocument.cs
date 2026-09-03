using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

/// <summary>
/// The Cosmos envelope an employee-facing leave-status notification is stored as.
/// One document per status change. DeliveredUtc is null while pending; the chat
/// runtime marks it once it has been shown to the employee.
/// </summary>
internal sealed class LeaveStatusNotificationDocument
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The employee's mobile number. Partition key path: /mobile.</summary>
    [JsonProperty("mobile")]
    public string Mobile { get; set; } = string.Empty;

    [JsonProperty("applicationReference")]
    public string ApplicationReference { get; set; } = string.Empty;

    [JsonProperty("newStatus")]
    public string NewStatus { get; set; } = string.Empty;

    [JsonProperty("note")]
    public string? Note { get; set; }

    [JsonProperty("createdUtc")]
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Null while pending. Set the moment the chat runtime delivers it.</summary>
    [JsonProperty("deliveredUtc")]
    public DateTimeOffset? DeliveredUtc { get; set; }

    /// <summary>
    /// Cosmos's reserved per-item TTL field, in seconds. Only takes effect if the
    /// container itself has TTL enabled (an Azure-side container setting, not
    /// something this code controls).
    /// </summary>
    [JsonProperty("ttl")]
    public int Ttl { get; set; }
}
