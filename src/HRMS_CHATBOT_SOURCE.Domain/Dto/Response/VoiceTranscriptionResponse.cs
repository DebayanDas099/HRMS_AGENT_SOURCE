using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class VoiceTranscriptionResponse
{
    [JsonProperty("english_text")]
    public string EnglishText { get; set; } = string.Empty;

    [JsonProperty("original_text")]
    public string? OriginalText { get; set; }

    [JsonProperty("detected_language")]
    public string? DetectedLanguage { get; set; }
}
