using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class VoiceInputEnabledResponse
{
    public const string DefaultDisabledMessage = "Voice feature is disabled for you.";

    [JsonProperty("voice_enabled")]
    public bool VoiceEnabled { get; set; }

    [JsonProperty("disabled_message")]
    public string DisabledMessage { get; set; } = DefaultDisabledMessage;
}
