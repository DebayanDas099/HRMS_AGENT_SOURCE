using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

/// <summary>
/// Implements <see cref="IAdminLeaveNotifier"/>. Called fire-and-forget from
/// LeaveApplicationTools right after a successful apply, so a failure here must
/// never surface back to the caller - caught and logged, not rethrown.
/// </summary>
public sealed class AdminLeaveNotifier : IAdminLeaveNotifier
{
    private readonly IAdminNotificationStore _store;
    private readonly ILogger<AdminLeaveNotifier> _logger;

    public AdminLeaveNotifier(IAdminNotificationStore store, ILogger<AdminLeaveNotifier> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task NotifyAppliedAsync(
        string? applicationReference,
        string employeeMobile,
        string? employeeName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _store.AddAsync(applicationReference, employeeMobile, employeeName, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record admin notification for leave application {Reference} from {Mobile}.",
                applicationReference,
                employeeMobile);
        }
    }
}
