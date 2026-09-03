namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public sealed class AzureSpeechSettings
{
    public const string SectionName = "AzureSpeech";

    public string? ApiKey { get; set; }

    public string? Endpoint { get; set; }

    public int MaxAudioSeconds { get; set; } = 60;

    public long MaxAudioBytes { get; set; } = 5_000_000;
}
