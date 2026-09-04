using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Domain.Interfaces;

public interface ISpeechTranslationService
{
    Task<VoiceTranscriptionResponse> TranscribeAndTranslateToEnglishAsync(
        Stream audioStream,
        string contentType,
        CancellationToken cancellationToken = default);
}
