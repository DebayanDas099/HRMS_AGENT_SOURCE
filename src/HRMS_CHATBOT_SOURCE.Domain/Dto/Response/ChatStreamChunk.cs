using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class ChatStreamChunk
{
    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
    public string? Text { get; set; }

    [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
    public string? Message { get; set; }

    [JsonProperty("conversation_id", NullValueHandling = NullValueHandling.Ignore)]
    public string? ConversationId { get; set; }

    [JsonProperty("reply", NullValueHandling = NullValueHandling.Ignore)]
    public string? Reply { get; set; }

    [JsonProperty("enabled_agents", NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<string>? EnabledAgents { get; set; }

    [JsonProperty("last_speaker", NullValueHandling = NullValueHandling.Ignore)]
    public string? LastSpeaker { get; set; }

    public static ChatStreamChunk Delta(string text) =>
        new() { Type = "delta", Text = text };

    public static ChatStreamChunk Done(
        string conversationId,
        string reply,
        IReadOnlyList<string> enabledAgents,
        string? lastSpeaker) =>
        new()
        {
            Type = "done",
            ConversationId = conversationId,
            Reply = reply,
            EnabledAgents = enabledAgents,
            LastSpeaker = lastSpeaker
        };

    public static ChatStreamChunk Error(string message) =>
        new() { Type = "error", Message = message };
}
