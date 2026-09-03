namespace HRMS_CHATBOT_SOURCE.Domain.Interfaces;

/// <summary>
/// Records that a leave application's status changed, for later delivery to the
/// employee. Declared here, not in the Agent project, so Logic can depend on it
/// without referencing Agent directly - the same split used for
/// <see cref="IHrmsChatRuntime"/>.
/// <para>
/// This is the seam a leave-approval action calls immediately after its own status
/// update commits - fire-and-forget from the caller's perspective. The
/// implementation is responsible for not letting a failure here propagate back to
/// the caller and for not touching the caller's DI scope (it resolves storage
/// through its own scope), so callers are free to invoke this without awaiting it.
/// </para>
/// </summary>
public interface ILeaveStatusChangeNotifier
{
    Task NotifyAsync(
        string mobile,
        string applicationReference,
        string newStatus,
        string? note,
        CancellationToken cancellationToken = default);
}
