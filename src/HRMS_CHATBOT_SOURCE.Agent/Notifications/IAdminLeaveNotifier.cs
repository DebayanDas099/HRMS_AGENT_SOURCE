namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

/// <summary>
/// The seam LeaveApplicationTools calls (fire-and-forget) the instant a leave
/// application is successfully submitted, so the admin bell reflects it. Only
/// consumed within the Agent project, unlike ILeaveStatusChangeNotifier which
/// Logic also needs - no Domain.Interfaces indirection required here.
/// </summary>
public interface IAdminLeaveNotifier
{
    Task NotifyAppliedAsync(
        string? applicationReference,
        string employeeMobile,
        string? employeeName,
        CancellationToken cancellationToken = default);
}
