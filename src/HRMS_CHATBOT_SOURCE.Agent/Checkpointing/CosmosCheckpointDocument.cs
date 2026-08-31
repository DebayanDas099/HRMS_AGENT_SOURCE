using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Agent.Checkpointing;

/// <summary>
/// The Cosmos envelope a checkpoint is stored as. The checkpoint's own payload
/// (a System.Text.Json JsonElement, per MAF's CheckpointManager.CreateJson contract)
/// is kept as its raw JSON text rather than a nested object: the Cosmos SDK's
/// serializer is Newtonsoft-based and has no built-in converter for
/// System.Text.Json.JsonElement, so round-tripping it as a string sidesteps a
/// serializer mismatch entirely regardless of exactly how the SDK serializes the
/// rest of the document.
/// </summary>
internal sealed class CosmosCheckpointDocument
{
    /// <summary>Cosmos's required unique-per-partition document key.</summary>
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>The conversation/session id. Partition key path: /threadId.</summary>
    [JsonProperty("threadId")]
    public string ThreadId { get; set; } = string.Empty;

    [JsonProperty("parentCheckpointId")]
    public string? ParentCheckpointId { get; set; }

    [JsonProperty("payload")]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Ordering key. ICheckpointStore's contract requires RetrieveIndexAsync to
    /// return checkpoints oldest-first - CheckpointManager relies on that order to
    /// find the latest one - so every index query sorts by this field ascending.
    /// </summary>
    [JsonProperty("committedUtc")]
    public DateTimeOffset CommittedUtc { get; set; }

    /// <summary>
    /// Cosmos's reserved per-item TTL field, in seconds. Only takes effect if the
    /// container itself has TTL enabled (an Azure-side container setting, not
    /// something this code controls).
    /// </summary>
    [JsonProperty("ttl")]
    public int Ttl { get; set; }
}
