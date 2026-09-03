using System.ComponentModel;
using System.Globalization;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent.Tools;

/// <summary>
/// The Leave Approval Agent's tools: list pending applications and submit one
/// approve/reject decision.
/// <para>
/// Only reachable when ChatLogic added LeaveApprovalAgent to the enabled-agent list
/// for this turn, which it only does after a server-verified admin claim on the
/// request (see ChatLogic.SendMessageAsync) - not a model-supplied or client-supplied
/// value. SubmitLeaveDecisionAsync re-checks that claim itself before acting, rather
/// than trusting the workflow wiring alone: defense in depth against a future wiring
/// mistake elsewhere putting this agent in a session that was never actually verified.
/// </para>
/// <para>
/// The admin identity used as "approved by" is resolved here, server-side, from
/// IServiceContext.CurrentUser - never accepted as a model-supplied argument, since
/// the model cannot be trusted to accurately relay who is really asking.
/// </para>
/// </summary>
public sealed class LeaveApprovalTools
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeaveApprovalTools> _logger;

    public LeaveApprovalTools(IServiceScopeFactory scopeFactory, ILogger<LeaveApprovalTools> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    [Description(
        "Lists every leave application currently awaiting an approve/reject decision, with each "
        + "application's reference, employee, dates, and reason. Call this before approving or "
        + "rejecting anything so you have the exact reference to use.")]
    public async Task<string> GetPendingLeaveApplicationsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var approvalLogic = scope.ServiceProvider.GetRequiredService<ILeaveApprovalLogic>();

        var pending = await approvalLogic.GetPendingApplicationsAsync(cancellationToken).ConfigureAwait(false);
        return FormatPending(pending);
    }

    [Description(
        "Approves or rejects one pending leave application by its reference (from "
        + "GetPendingLeaveApplicationsAsync). Remarks are optional when approving but REQUIRED when "
        + "rejecting - if the admin wants to reject and has not given a reason yet, ask for one before "
        + "calling this tool. Never call this with decision=Rejected and empty remarks.")]
    public async Task<string> SubmitLeaveDecisionAsync(
        [Description("The application reference exactly as shown in the pending list.")] string applicationReference,
        [Description("'Approved' or 'Rejected'.")] string decision,
        [Description("Reason/remarks for the decision. Optional when approving, required when rejecting.")] string? remarks,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var serviceContext = scope.ServiceProvider.GetRequiredService<IServiceContext>();

        if (!string.Equals(serviceContext.CurrentUser?.IsAdmin, "Y", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("SubmitLeaveDecisionAsync was invoked without a verified admin session.");
            return "You are not authorized to approve or reject leave applications.";
        }

        var approvalLogic = scope.ServiceProvider.GetRequiredService<ILeaveApprovalLogic>();
        var approvedBy = serviceContext.CurrentUser?.UserId ?? serviceContext.CurrentUser?.FullName ?? "admin";

        var processed = await approvalLogic.SubmitDecisionsAsync(
            [new LeaveApprovalDecisionDto { ApplicationReference = applicationReference, Decision = decision, Remarks = remarks }],
            approvedBy,
            cancellationToken).ConfigureAwait(false);

        if (processed.Count == 0)
        {
            if (string.Equals(decision, "Rejected", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(remarks))
            {
                return "Remarks are required to reject an application. Ask the admin for a reason and try again.";
            }

            return $"Could not process application {applicationReference} - it may no longer be awaiting a decision.";
        }

        return string.IsNullOrWhiteSpace(remarks)
            ? $"Application {applicationReference} was {decision.Trim()} successfully."
            : $"Application {applicationReference} was {decision.Trim()} successfully. Remarks: {remarks.Trim()}";
    }

    private static string FormatPending(IReadOnlyList<PendingLeaveApplicationDto> pending)
    {
        if (pending.Count == 0)
        {
            return "No leave applications are currently awaiting a decision.";
        }

        var lines = pending.Select(p =>
            $"- {p.ApplicationReference}: {p.EmployeeName ?? p.Mobile} - "
            + $"{p.FromDate.ToString(DateFormat, CultureInfo.InvariantCulture)} to {p.ToDate.ToString(DateFormat, CultureInfo.InvariantCulture)}, "
            + $"reason: {p.Reason ?? "N/A"}, applied {p.AppliedOn.ToString(DateFormat, CultureInfo.InvariantCulture)}");

        return "Pending leave applications:" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }
}
