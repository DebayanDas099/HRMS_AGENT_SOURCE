using System.Globalization;
using System.Text.Json;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;

namespace HRMS_CHATBOT_SOURCE.Logic.Common;

public sealed class CommonLogic : ICommonLogic
{
    private const string DateFormat = "yyyy-MM-dd";
    private static readonly TimeZoneInfo IstZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    public DateTime GetReferenceDateTime(DateTime? utcNow = null)
    {
        var utc = utcNow ?? DateTime.UtcNow;
        return TimeZoneInfo.ConvertTimeFromUtc(utc, IstZone);
    }

    public RelativeDateParseResult ParseRelativeDate(string? phrase, DateTime? referenceDate = null)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return NotFound(phrase ?? string.Empty, "No date phrase was provided.");
        }

        var reference = referenceDate ?? GetReferenceDateTime();
        var trimmed = phrase.Trim();

        var matches = DateTimeRecognizer.RecognizeDateTime(
            trimmed,
            Culture.English,
            DateTimeOptions.None,
            reference);

        if (matches == null || matches.Count == 0)
        {
            return NotFound(trimmed, "No date could be recognized in the phrase.");
        }

        var match = matches.FirstOrDefault(m =>
            string.Equals(m.TypeName, "daterange", StringComparison.OrdinalIgnoreCase))
            ?? matches[0];

        if (!TryExtractRange(match, out var start, out var end, out var type))
        {
            return NotFound(trimmed, "Date was recognized but start/end could not be resolved.");
        }

        return new RelativeDateParseResult
        {
            Found = true,
            Phrase = trimmed,
            StartDate = start?.ToString(DateFormat, CultureInfo.InvariantCulture),
            EndDate = end?.ToString(DateFormat, CultureInfo.InvariantCulture),
            Type = type,
            Message = "Date range resolved successfully."
        };
    }

    public RelativeDateParseResult ParseRelativeDateFromUserMessage(string? message, DateTime? referenceDate = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return NotFound(string.Empty, "No user message to parse.");
        }

        return ParseRelativeDate(message.Trim(), referenceDate);
    }

    private static bool TryExtractRange(
        ModelResult match,
        out DateTime? start,
        out DateTime? end,
        out string? type)
    {
        start = null;
        end = null;
        type = match.TypeName;

        if (match.Resolution == null)
        {
            return false;
        }

        var resolutionJson = JsonSerializer.Serialize(match.Resolution);
        if (string.IsNullOrWhiteSpace(resolutionJson))
        {
            return false;
        }

        using var doc = JsonDocument.Parse(resolutionJson);
        if (!doc.RootElement.TryGetProperty("values", out var values)
            || values.GetArrayLength() == 0)
        {
            return false;
        }

        var first = values[0];

        if (first.TryGetProperty("start", out var startProp)
            && DateTime.TryParse(startProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStart))
        {
            start = parsedStart.Date;
        }

        if (first.TryGetProperty("end", out var endProp)
            && DateTime.TryParse(endProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedEnd))
        {
            end = parsedEnd.Date;
        }

        if (start == null
            && first.TryGetProperty("value", out var valueProp)
            && DateTime.TryParse(valueProp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var single))
        {
            start = single.Date;
            end = single.Date;
        }

        if (first.TryGetProperty("type", out var typeProp))
        {
            type = typeProp.GetString();
        }

        if (string.Equals(type, "daterange", StringComparison.OrdinalIgnoreCase)
            && end != null
            && (start == null || end > start))
        {
            end = end.Value.AddDays(-1);
        }

        return start != null || end != null;
    }

    private static RelativeDateParseResult NotFound(string phrase, string message)
    {
        return new RelativeDateParseResult
        {
            Found = false,
            Phrase = phrase,
            Message = message
        };
    }
}
