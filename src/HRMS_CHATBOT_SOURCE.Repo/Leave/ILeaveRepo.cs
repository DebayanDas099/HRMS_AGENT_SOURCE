using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Repo.Leave;

public interface ILeaveRepo
{
    Task<MSSQLResponse?> GetLeaveDetailsByUserAsync(
        string? mobile,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> ValidateAndApplyLeaveByUserAsync(
        string? mobile,
        DateTime startDate,
        DateTime endDate,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every leave application still awaiting an approve/reject decision.
    /// Backed by stored procedure [dbo].[Get_Pending_Leave_Applications] - this is a
    /// new procedure, not yet provisioned in the HRMS SQL database as of this writing.
    /// </summary>
    Task<MSSQLResponse?> GetPendingLeaveApplicationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an approve/reject decision against a leave application.
    /// Backed by stored procedure [dbo].[Update_Leave_Application_Status] - this is a
    /// new procedure, not yet provisioned in the HRMS SQL database as of this writing.
    /// </summary>
    Task<MSSQLResponse?> UpdateLeaveApplicationStatusAsync(
        string applicationReference,
        string newStatus,
        string approvedBy,
        string? note,
        CancellationToken cancellationToken = default);
}
