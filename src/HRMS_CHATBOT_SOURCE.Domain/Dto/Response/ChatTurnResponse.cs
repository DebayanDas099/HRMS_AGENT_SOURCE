using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class ChatTurnResponse
{
    [JsonProperty("conversation_id")]
    public string ConversationId { get; set; } = string.Empty;

    [JsonProperty("reply")]
    public string Reply { get; set; } = string.Empty;

    [JsonProperty("enabled_agents")]
    public IReadOnlyList<string> EnabledAgents { get; set; } = [];

    [JsonProperty("last_speaker")]
    public string? LastSpeaker { get; set; }
}
