using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface ILeaveApprovalLogic
{
    Task<List<PendingLeaveApplicationDto>> GetPendingApplicationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists each decision and fires (without awaiting) the employee-facing
    /// notification for it. Returns the references that were actually processed -
    /// a decision naming an application that no longer exists (already actioned by
    /// someone else) is skipped rather than failing the whole batch.
    /// </summary>
    Task<List<string>> SubmitDecisionsAsync(
        IReadOnlyList<LeaveApprovalDecisionDto> decisions,
        string approvedByAdminId,
        CancellationToken cancellationToken = default);
}
