using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HRMS_CHATBOT_SOURCE.Agent.Notifications;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent.Tools;

/// <summary>
/// The Leave Application Agent's tools: leave balance lookup and leave application
/// submission, and company holiday lookup.
/// <para>
/// The chat runtime never resolves a caller's HRMS user id - only the mobile number
/// captured on the chat request ever reaches this layer, and that number is not
/// threaded down into tool invocations. So the model is asked for the employee's
/// mobile number, and the backend (stored procedure) resolves the HRMS user id from
/// it, mirroring how <c>IAgentAccessService</c> already resolves enabled agents by
/// mobile.
/// </para>
/// </summary>
public sealed class LeaveApplicationTools
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAdminLeaveNotifier _adminLeaveNotifier;
    private readonly ILogger<LeaveApplicationTools> _logger;

    public LeaveApplicationTools(
        IServiceScopeFactory scopeFactory,
        IAdminLeaveNotifier adminLeaveNotifier,
        ILogger<LeaveApplicationTools> logger)
    {
        _scopeFactory = scopeFactory;
        _adminLeaveNotifier = adminLeaveNotifier;
        _logger = logger;
    }

    [Description(
        "Gets the employee's leave balance summary (accrued, applied, remaining, loss of pay, "
        + "contract status) for the given mobile number over one session date range. Defaults to "
        + "the current month when dates are omitted. This tool covers a single range per call; if "
        + "several date ranges were parsed, invoke it once per range. Ask the employee for their "
        + "registered mobile number if you do not already have it.")]
    public async Task<string> GetLeaveStatusAsync(
        [Description("The employee's registered mobile number.")] string mobile,
        [Description("Session start date (yyyy-MM-dd). Defaults to the first day of the current month.")] string? startDate = null,
        [Description("Session end date (yyyy-MM-dd). Defaults to the last day of the current month.")] string? endDate = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // The workflow factory is a singleton and leave data access is scoped, so a
            // scope is opened per invocation rather than capturing a scoped service in a
            // singleton (same approach as PolicyKnowledgeTools).
            using var scope = _scopeFactory.CreateScope();
            var leaveLogic = scope.ServiceProvider.GetRequiredService<ILeaveLogic>();

            var start = ParseOptionalDate(startDate);
            var end = ParseOptionalDate(endDate);

            var summary = await leaveLogic
                .GetLeaveBalanceSummaryAsync(mobile, start, end, cancellationToken)
                .ConfigureAwait(false);

            return FormatSummary(summary);
        }
        catch (ValidationException ex)
        {
            _logger.LogInformation("Leave balance lookup rejected for mobile {Mobile}: {Reason}", mobile, ex.Message);
            return $"Unable to fetch leave balance: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Leave balance lookup failed for mobile {Mobile}.", mobile);
            return "The leave system could not be reached. Tell the employee the service is temporarily unavailable and to try again shortly.";
        }
    }

    [Description(
        "Validates and submits a leave application for the employee's registered mobile number. "
        + "Requires from date, to date, leave type, and reason - collect all four from the employee "
        + "before calling. Leave type examples: casual, sick, earned, loss of pay. Ask for the "
        + "registered mobile number only when it is not already known from the session context.")]
    public async Task<string> ValidateAndApplyLeaveAsync(
        [Description("The employee's registered mobile number.")] string mobile,
        [Description("Leave start date (yyyy-MM-dd). Required.")] string fromDate,
        [Description("Leave end date (yyyy-MM-dd). Required.")] string toDate,
        [Description("Leave type (e.g. casual, sick, earned, loss of pay). Required.")] string leaveType,
        [Description("Reason for the leave request. Required.")] string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var leaveLogic = scope.ServiceProvider.GetRequiredService<ILeaveLogic>();

            var result = await leaveLogic
                .ValidateAndApplyLeaveAsync(
                    mobile,
                    ParseRequiredDate(fromDate),
                    ParseRequiredDate(toDate),
                    leaveType,
                    reason,
                    cancellationToken)
                .ConfigureAwait(false);

            // Fire-and-forget by design: the chat turn should not wait on the admin
            // notification. NotifyAppliedAsync resolves its own storage scope and
            // catches/logs internally, so it is safe to outlive this scope.
            _ = _adminLeaveNotifier.NotifyAppliedAsync(applicationReference: null, mobile, employeeName: null, CancellationToken.None);

            return result;
        }
        catch (ValidationException ex)
        {
            _logger.LogInformation("Leave application rejected for mobile {Mobile}: {Reason}", mobile, ex.Message);
            return $"Unable to validate leave request: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Leave application failed for mobile {Mobile}.", mobile);
            return "The leave system could not be reached. Tell the employee the service is temporarily unavailable and to try again shortly.";
        }
    }

    [Description(
        "Gets company holidays for a date range. Use for holiday list questions. "
        + "For month/year questions, parse the phrase with ParseRelativeDateRange first. "
        + "For 'next holiday' or 'upcoming holidays', set startDate to today, endDate to year-end, "
        + "and maxResults to 1 (or N). Do not fetch the full year and filter yourself.")]
    public async Task<string> GetHolidayListAsync(
        [Description("Range start (yyyy-MM-dd). Defaults to Jan 1 of current year, or today when maxResults is set.")] string? startDate = null,
        [Description("Range end (yyyy-MM-dd). Defaults to Dec 31 of current year.")] string? endDate = null,
        [Description("Max holidays to return. Use 1 for 'next holiday', 3 for 'next 3 holidays'. Omit for full list in range.")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var leaveLogic = scope.ServiceProvider.GetRequiredService<ILeaveLogic>();

            var holidays = await leaveLogic
                .GetHolidayListAsync(
                    ParseOptionalDate(startDate),
                    ParseOptionalDate(endDate),
                    maxResults,
                    cancellationToken)
                .ConfigureAwait(false);

            return FormatHolidayList(holidays, startDate, endDate, maxResults);
        }
        catch (ValidationException ex)
        {
            _logger.LogInformation("Holiday list lookup rejected: {Reason}", ex.Message);
            return $"Unable to fetch holiday list: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Holiday list lookup failed.");
            return "The leave system could not be reached. Tell the employee the service is temporarily unavailable and to try again shortly.";
        }
    }

    private static string FormatHolidayList(
        IReadOnlyList<HolidayListItemDto> holidays,
        string? startDate,
        string? endDate,
        int? maxResults)
    {
        if (holidays.Count == 0)
        {
            return "No holidays found for the requested period.";
        }

        if (maxResults == 1)
        {
            var next = holidays[0];
            var typeSuffix = string.IsNullOrWhiteSpace(next.HolidayType) ? string.Empty : $" ({next.HolidayType})";
            return $"Next company holiday:{Environment.NewLine}"
                + $"- {next.HolidayDate.ToString(DateFormat, CultureInfo.InvariantCulture)}: {next.HolidayName}{typeSuffix}";
        }

        var rangeStart = holidays[0].HolidayDate.ToString(DateFormat, CultureInfo.InvariantCulture);
        var rangeEnd = holidays[^1].HolidayDate.ToString(DateFormat, CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(startDate) && !string.IsNullOrWhiteSpace(endDate))
        {
            rangeStart = startDate.Trim();
            rangeEnd = endDate.Trim();
        }

        var lines = holidays.Select(h =>
        {
            var typeSuffix = string.IsNullOrWhiteSpace(h.HolidayType) ? string.Empty : $" ({h.HolidayType})";
            return $"- {h.HolidayDate.ToString(DateFormat, CultureInfo.InvariantCulture)}: {h.HolidayName}{typeSuffix}";
        });

        var heading = maxResults.HasValue
            ? $"Upcoming company holidays (showing up to {maxResults.Value}):"
            : $"Company holidays ({rangeStart} to {rangeEnd}):";

        return heading + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }

    private static string FormatSummary(LeaveBalanceSummaryDto summary)
    {
        return $"Leave balance summary for {summary.EmpId}: "
            + $"accrued={summary.AccruedLeaveBalance}, "
            + $"applied={summary.AppliedLeave}, "
            + $"remaining={summary.RemainingLeaveBalance}, "
            + $"loss_of_pay={summary.LossOfPay}, "
            + $"contract_status={summary.ContractStatus ?? "N/A"}, "
            + $"contract_end_date={summary.ContractEndDate?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? "N/A"}.";
    }

    private static DateTime? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new ValidationException($"Invalid date '{value}'. Use format {DateFormat}.");
    }

    private static DateTime ParseRequiredDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"Date is required. Use format {DateFormat}.");
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new ValidationException($"Invalid date '{value}'. Use format {DateFormat}.");
    }
}
