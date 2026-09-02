using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Repo.Admin;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Logic.Common;

public sealed class CommonLogic : ICommonLogic
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string MailApiUrl = "https://bpilmobile.bergerindia.com/mccapis/email/v1/send";
    private static readonly TimeZoneInfo IstZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
    private readonly IUserProfileRepo? _userProfileRepo;
    private readonly Microsoft.Extensions.Configuration.IConfiguration? _configuration;
    private readonly ILogger<CommonLogic>? _logger;

    public CommonLogic(
        IUserProfileRepo? userProfileRepo = null,
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null,
        ILogger<CommonLogic>? logger = null)
    {
        _userProfileRepo = userProfileRepo;
        _configuration = configuration;
        _logger = logger;
    }

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
        var normalized = NormalizePhrase(trimmed);

        var matches = DateTimeRecognizer.RecognizeDateTime(
            normalized,
            Culture.English,
            DateTimeOptions.None,
            reference);

        if (matches == null || matches.Count == 0)
        {
            return NotFound(trimmed, "No date could be recognized in the phrase.");
        }

        var extracted = new List<(DateTime Start, DateTime End, string? Type)>();
        foreach (var match in matches)
        {
            if (!TryExtractRange(match, out var rangeStart, out var rangeEnd, out var type))
            {
                continue;
            }

            if (rangeStart == null && rangeEnd == null)
            {
                continue;
            }

            var start = (rangeStart ?? rangeEnd)!.Value.Date;
            var end = (rangeEnd ?? rangeStart)!.Value.Date;
            extracted.Add((start, end, type));
        }

        var ranges = StabilizeRanges(extracted);

        return new RelativeDateParseResult
        {
            Found = ranges.Count > 0,
            Phrase = trimmed,
            Message = ranges.Count > 1
                    ? $"Resolved {ranges.Count} date ranges. Call leave balance once per range."
                    : "Date range resolved successfully.",
            Ranges = ranges
        };
    }

    private static IReadOnlyList<RelativeDateRangeDto> StabilizeRanges(
        IReadOnlyList<(DateTime Start, DateTime End, string? Type)> extracted)
    {
        if (extracted.Count == 0)
        {
            return [];
        }

        var expandSingleDays = extracted.Count > 1;
        var unique = new List<(DateTime Start, DateTime End, string? Type)>();

        foreach (var item in extracted.OrderBy(r => r.Start).ThenBy(r => r.End))
        {
            var start = item.Start;
            var end = item.End;
            if (expandSingleDays && start == end)
            {
                start = new DateTime(start.Year, start.Month, 1);
                end = new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
            }

            if (unique.Any(existing => existing.Start == start && existing.End == end))
            {
                continue;
            }

            unique.Add((start, end, item.Type));
        }

        return unique
            .Select(item => new RelativeDateRangeDto
            {
                StartDate = item.Start.ToString(DateFormat, CultureInfo.InvariantCulture),
                EndDate = item.End.ToString(DateFormat, CultureInfo.InvariantCulture),
                Type = item.Type,
                Label = item.Start.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            })
            .ToList();
    }

    public RelativeDateParseResult ParseRelativeDateFromUserMessage(string? message, DateTime? referenceDate = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return NotFound(string.Empty, "No user message to parse.");
        }

        return ParseRelativeDate(message.Trim(), referenceDate);
    }

    public async Task<string?> GetUserEmailByMobileAsync(string? mobile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobile) || _userProfileRepo == null)
        {
            return null;
        }

        return await _userProfileRepo
            .GetUserEmailByMobileAsync(mobile.Trim(), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> SendMailNewAsync(
        string toAddress,
        string mailSubject,
        string mailBody,
        string? attachmentPath = null,
        string? ccAddress = null,
        string? bccAddress = null,
        string? fromAddress = null,
        string? senderApp = null,
        string? senderTask = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toAddress))
        {
            return 0;
        }

        var authToken = _configuration?["MCCWebAPIAuthToken"];
        if (string.IsNullOrWhiteSpace(authToken))
        {
            _logger?.LogWarning("MCCWebAPIAuthToken is missing, mail send skipped.");
            return 0;
        }

        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            var payload = new
            {
                mailFromAddress = string.IsNullOrWhiteSpace(fromAddress)
                    ? (_configuration?["AppSettings:MailFromAddress"] ?? "noreply@mccit.co.in")
                    : fromAddress.Trim(),
                mailToAddress = toAddress.Trim(),
                mailCCAddress = ccAddress ?? string.Empty,
                mailBCCAddress = bccAddress ?? string.Empty,
                mailSubject = mailSubject ?? string.Empty,
                mailBody = mailBody ?? string.Empty,
                mailAttachement = attachmentPath ?? string.Empty,
                mailSenderApp = string.IsNullOrWhiteSpace(senderApp) ? "HRMS_CHATBOT_SOURCE" : senderApp.Trim(),
                mailSenderTask = string.IsNullOrWhiteSpace(senderTask) ? "DocumentAgent" : senderTask.Trim()
            };

            var postData = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, MailApiUrl)
            {
                Content = new StringContent(postData, Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken.Trim());

            using var client = new HttpClient();
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                return 0;
            }

            using var json = JsonDocument.Parse(responseJson);
            if (json.RootElement.TryGetProperty("responseCode", out var responseCode)
                && responseCode.TryGetInt32(out var code))
            {
                return code;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Mail API call failed for recipient {ToAddress}.", toAddress);
            return 0;
        }
    }

    private static string NormalizePhrase(string phrase)
    {
        var text = phrase.Replace("current and last month", "this month and last month", StringComparison.OrdinalIgnoreCase);
        text = text.Replace("this and last month", "this month and last month", StringComparison.OrdinalIgnoreCase);
        return text.Replace("current month", "this month", StringComparison.OrdinalIgnoreCase);
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
