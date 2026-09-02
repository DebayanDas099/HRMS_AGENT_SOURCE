using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent.Tools;

/// <summary>
/// The Leave Application Agent's tools: leave balance lookup and leave application
/// submission.
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
    private readonly ILogger<LeaveApplicationTools> _logger;

    public LeaveApplicationTools(IServiceScopeFactory scopeFactory, ILogger<LeaveApplicationTools> logger)
    {
        _scopeFactory = scopeFactory;
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
        + "Requires from date, to date, and reason - collect all three from the employee before "
        + "calling. Ask for the registered mobile number only when it is not already known from "
        + "the session context.")]
    public async Task<string> ValidateAndApplyLeaveAsync(
        [Description("The employee's registered mobile number.")] string mobile,
        [Description("Leave start date (yyyy-MM-dd). Required.")] string fromDate,
        [Description("Leave end date (yyyy-MM-dd). Required.")] string toDate,
        [Description("Reason for the leave request. Required.")] string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var leaveLogic = scope.ServiceProvider.GetRequiredService<ILeaveLogic>();

            return await leaveLogic
                .ValidateAndApplyLeaveAsync(
                    mobile,
                    ParseRequiredDate(fromDate),
                    ParseRequiredDate(toDate),
                    reason,
                    cancellationToken)
                .ConfigureAwait(false);
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
