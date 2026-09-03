using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Logic.Adapter;
using HRMS_CHATBOT_SOURCE.Repo.Leave;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Logic;

public class LeaveApprovalLogic : ILeaveApprovalLogic
{
    private const string ApprovedStatus = "Approved";
    private const string RejectedStatus = "Rejected";

    private readonly ILeaveRepo _leaveRepo;
    private readonly ILeaveStatusChangeNotifier _notifier;
    private readonly ILogger<LeaveApprovalLogic> _logger;

    public LeaveApprovalLogic(ILeaveRepo leaveRepo, ILeaveStatusChangeNotifier notifier, ILogger<LeaveApprovalLogic> logger)
    {
        _leaveRepo = leaveRepo;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<List<PendingLeaveApplicationDto>> GetPendingApplicationsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _leaveRepo.GetPendingLeaveApplicationsAsync(cancellationToken).ConfigureAwait(false);
        return LeaveAdapter.MapPendingApplications(response);
    }

    public async Task<List<string>> SubmitDecisionsAsync(
        IReadOnlyList<LeaveApprovalDecisionDto> decisions,
        string approvedByAdminId,
        CancellationToken cancellationToken = default)
    {
        var processed = new List<string>();
        if (decisions.Count == 0)
        {
            return processed;
        }

        // Pending applications are the only place this layer knows an application's
        // mobile number - needed to address the employee-facing notification. Also
        // doubles as the "does this reference still need a decision" check: a
        // reference someone else already actioned since the grid was loaded is
        // silently skipped rather than failing the whole batch.
        var pending = await GetPendingApplicationsAsync(cancellationToken).ConfigureAwait(false);
        var byReference = pending.ToDictionary(p => p.ApplicationReference, StringComparer.OrdinalIgnoreCase);

        foreach (var decision in decisions)
        {
            if (!byReference.TryGetValue(decision.ApplicationReference, out var application))
            {
                _logger.LogInformation(
                    "Skipped decision for {Reference}: no longer awaiting approval.",
                    decision.ApplicationReference);
                continue;
            }

            var status = NormalizeStatus(decision.Decision);
            if (status is null)
            {
                _logger.LogWarning(
                    "Skipped decision for {Reference}: unrecognized decision '{Decision}'.",
                    decision.ApplicationReference,
                    decision.Decision);
                continue;
            }

            if (status == RejectedStatus && string.IsNullOrWhiteSpace(decision.Remarks))
            {
                // The grid enforces this client-side already; re-checked here because a
                // client-side-only rule is not a real guarantee.
                _logger.LogWarning(
                    "Skipped decision for {Reference}: remarks are required to reject.",
                    decision.ApplicationReference);
                continue;
            }

            var remarks = string.IsNullOrWhiteSpace(decision.Remarks) ? null : decision.Remarks.Trim();

            var response = await _leaveRepo
                .UpdateLeaveApplicationStatusAsync(decision.ApplicationReference, status, approvedByAdminId, remarks, cancellationToken)
                .ConfigureAwait(false);
            LeaveAdapter.EnsureSuccess(response);

            // Fire-and-forget by design: the admin's submit request should not wait on
            // notification delivery. NotifyAsync resolves its own storage scope and
            // catches/logs internally, so it is safe to outlive this request.
            _ = _notifier.NotifyAsync(application.Mobile, decision.ApplicationReference, status, remarks, CancellationToken.None);

            processed.Add(decision.ApplicationReference);
        }

        return processed;
    }

    private static string? NormalizeStatus(string decision)
    {
        if (string.Equals(decision, "Approved", StringComparison.OrdinalIgnoreCase) || string.Equals(decision, "A", StringComparison.OrdinalIgnoreCase))
        {
            return ApprovedStatus;
        }

        if (string.Equals(decision, "Rejected", StringComparison.OrdinalIgnoreCase) || string.Equals(decision, "R", StringComparison.OrdinalIgnoreCase))
        {
            return RejectedStatus;
        }

        return null;
    }
}
