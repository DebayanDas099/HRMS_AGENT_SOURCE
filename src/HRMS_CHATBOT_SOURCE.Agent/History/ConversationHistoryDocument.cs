using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Agent.History;

/// <summary>
/// The Cosmos envelope a conversation's transcript is stored as. One document per
/// conversation, upserted whole on every turn (matches the snapshot-then-replace
/// semantics the runtime already used for its in-process cache).
/// </summary>
internal sealed class ConversationHistoryDocument
{
    /// <summary>Cosmos's required unique-per-partition document key. Equal to ConversationId.</summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The conversation id. Partition key path: /conversationId.</summary>
    [JsonProperty("conversationId")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonProperty("messages")]
    public List<StoredChatMessage> Messages { get; set; } = [];

    [JsonProperty("updatedUtc")]
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>
    /// Cosmos's reserved per-item TTL field, in seconds. Only takes effect if the
    /// container itself has TTL enabled (an Azure-side container setting, not
    /// something this code controls).
    /// </summary>
    [JsonProperty("ttl")]
    public int Ttl { get; set; }
}

internal sealed class StoredChatMessage
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("text")]
    public string Text { get; set; } = string.Empty;
}
