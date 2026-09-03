using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

/// <summary>
/// Implements <see cref="ILeaveStatusChangeNotifier"/>. Callers invoke this
/// fire-and-forget (started, not awaited) the instant a leave application's status
/// changes, so a failure here must never surface back to the caller - it is caught
/// and logged, not rethrown. The store it writes to already resolves Cosmos through
/// its own DI scope per call, so this notifier is safe to outlive the request scope
/// that triggered it.
/// </summary>
public sealed class LeaveStatusChangeNotifier : ILeaveStatusChangeNotifier
{
    private readonly ILeaveStatusNotificationStore _store;
    private readonly ILogger<LeaveStatusChangeNotifier> _logger;

    public LeaveStatusChangeNotifier(ILeaveStatusNotificationStore store, ILogger<LeaveStatusChangeNotifier> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task NotifyAsync(
        string mobile,
        string applicationReference,
        string newStatus,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            _logger.LogWarning(
                "Skipped leave status notification for application {Reference}: no mobile number was supplied.",
                applicationReference);
            return;
        }

        try
        {
            await _store.AddAsync(mobile, applicationReference, newStatus, note, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record leave status notification for {Mobile}, application {Reference}.",
                mobile,
                applicationReference);
        }
    }
}
