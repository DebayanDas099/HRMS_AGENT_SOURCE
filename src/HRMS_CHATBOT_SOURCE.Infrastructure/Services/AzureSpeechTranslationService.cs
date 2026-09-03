using System.ComponentModel.DataAnnotations;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Infrastructure.Services;

public sealed class AzureSpeechTranslationService : ISpeechTranslationService
{
    private const string TargetLanguage = "en";
    private const string PlaceholderSourceLanguage = "en-US";

    private static readonly string[] SourceLanguages = ["bn-IN", "hi-IN", "en-US"];

    private readonly AzureSpeechSettings _settings;
    private readonly ApplicationSecrets _secrets;
    private readonly ILogger<AzureSpeechTranslationService> _logger;

    public AzureSpeechTranslationService(
        IOptions<AzureSpeechSettings> settings,
        ApplicationSecrets secrets,
        ILogger<AzureSpeechTranslationService> logger)
    {
        _settings = settings.Value;
        _secrets = secrets;
        _logger = logger;
    }

    public async Task<VoiceTranscriptionResponse> TranscribeAndTranslateToEnglishAsync(
        Stream audioStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var apiKey = ResolveApiKey();
        var regionOrEndpoint = ResolveEndpoint();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(regionOrEndpoint))
        {
            throw new ValidationException("Azure Speech is not configured.");
        }

        await using var memory = new MemoryStream();
        await audioStream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
        memory.Position = 0;

        var translationConfig = CreateTranslationConfig(apiKey, regionOrEndpoint);
        translationConfig.SpeechRecognitionLanguage = PlaceholderSourceLanguage;
        translationConfig.AddTargetLanguage(TargetLanguage);

        var autoDetectConfig = AutoDetectSourceLanguageConfig.FromLanguages(SourceLanguages);

        using var pushStream = AudioInputStream.CreatePushStream();
        using var audioConfig = AudioConfig.FromStreamInput(pushStream);
        using var recognizer = new TranslationRecognizer(translationConfig, autoDetectConfig, audioConfig);

        var recognizeTask = recognizer.RecognizeOnceAsync();
        pushStream.Write(memory.ToArray());
        pushStream.Close();

        var result = await recognizeTask.ConfigureAwait(false);

        if (result.Reason == ResultReason.RecognizedSpeech
            || result.Reason == ResultReason.TranslatedSpeech)
        {
            var english = result.Translations.TryGetValue(TargetLanguage, out var translated)
                ? translated
                : result.Text;

            if (string.IsNullOrWhiteSpace(english))
            {
                throw new ValidationException("Could not transcribe the audio. Please try again.");
            }

            var detected = result.Properties.GetProperty(PropertyId.SpeechServiceConnection_AutoDetectSourceLanguageResult);

            return new VoiceTranscriptionResponse
            {
                EnglishText = english.Trim(),
                OriginalText = string.IsNullOrWhiteSpace(result.Text) ? null : result.Text.Trim(),
                DetectedLanguage = string.IsNullOrWhiteSpace(detected) ? null : detected
            };
        }

        if (result.Reason == ResultReason.NoMatch)
        {
            throw new ValidationException("Could not understand the audio. Please speak clearly and try again.");
        }

        if (result.Reason == ResultReason.Canceled)
        {
            var details = CancellationDetails.FromResult(result);
            _logger.LogWarning(
                "Azure Speech canceled: error={Error} detail={Detail}",
                details.ErrorCode,
                details.ErrorDetails);

            var message = details.ErrorCode switch
            {
                CancellationErrorCode.AuthenticationFailure => "Azure Speech authentication failed.",
                CancellationErrorCode.ConnectionFailure => "Azure Speech connection failed. Check the speech region/endpoint configuration.",
                _ when !string.IsNullOrWhiteSpace(details.ErrorDetails)
                    => details.ErrorDetails,
                _ => "Speech recognition failed. Please try again."
            };

            throw new ValidationException(message);
        }

        throw new ValidationException("Speech recognition failed. Please try again.");
    }

    private SpeechTranslationConfig CreateTranslationConfig(string apiKey, string regionOrEndpoint)
    {
        var value = regionOrEndpoint.Trim();

        if (value.StartsWith("wss://", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(value, UriKind.Absolute, out var wssUri))
        {
            if (IsUniversalV2Endpoint(wssUri))
            {
                return SpeechTranslationConfig.FromEndpoint(wssUri, apiKey);
            }

            var regionFromWss = ExtractRegionFromHost(wssUri.Host);
            if (!string.IsNullOrWhiteSpace(regionFromWss))
            {
                return CreateV2TranslationConfig(apiKey, regionFromWss);
            }

            throw new ValidationException(
                "Azure Speech endpoint must be a region name (e.g. swedencentral) or a wss:// universal/v2 endpoint.");
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var httpUri)
            && (httpUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                || httpUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)))
        {
            if (IsUniversalV2Endpoint(httpUri))
            {
                var wssFromHttp = new Uri(httpUri.ToString()
                    .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
                    .Replace("http://", "wss://", StringComparison.OrdinalIgnoreCase));

                return SpeechTranslationConfig.FromEndpoint(wssFromHttp, apiKey);
            }

            var regionFromHost = ExtractRegionFromHost(httpUri.Host);
            if (!string.IsNullOrWhiteSpace(regionFromHost))
            {
                return CreateV2TranslationConfig(apiKey, regionFromHost);
            }

            throw new ValidationException(
                "Azure Speech endpoint must be a region name (e.g. swedencentral) or a universal/v2 speech endpoint.");
        }

        return CreateV2TranslationConfig(apiKey, value);
    }

    private SpeechTranslationConfig CreateV2TranslationConfig(string apiKey, string region)
    {
        var v2Endpoint = BuildUniversalV2Endpoint(region);
        _logger.LogDebug(
            "Azure Speech auto-detect translation using v2 endpoint {Endpoint} in region {Region}.",
            v2Endpoint,
            region);

        return SpeechTranslationConfig.FromEndpoint(v2Endpoint, apiKey);
    }

    private static Uri BuildUniversalV2Endpoint(string region) =>
        new($"wss://{region.Trim().ToLowerInvariant()}.stt.speech.microsoft.com/speech/universal/v2");

    private static bool IsUniversalV2Endpoint(Uri uri) =>
        uri.AbsolutePath.Contains("/speech/universal/v2", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractRegionFromHost(string host)
    {
        // swedencentral.api.cognitive.microsoft.com
        // swedencentral.stt.speech.microsoft.com
        // swedencentral.s2s.speech.microsoft.com
        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        if (parts[1] is "api" or "stt" or "s2s")
        {
            return parts[0];
        }

        return null;
    }

    private string? ResolveApiKey() =>
        !string.IsNullOrWhiteSpace(_settings.ApiKey) ? _settings.ApiKey : _secrets.AzureSpeechApiKey;

    private string? ResolveEndpoint() =>
        !string.IsNullOrWhiteSpace(_settings.Endpoint) ? _settings.Endpoint : _secrets.AzureSpeechEndpoint;
}
