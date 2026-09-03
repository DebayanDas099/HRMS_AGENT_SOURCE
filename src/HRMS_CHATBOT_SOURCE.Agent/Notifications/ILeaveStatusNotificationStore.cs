namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

public sealed record PendingLeaveStatusNotification(
    string Id,
    string ApplicationReference,
    string NewStatus,
    string? Note,
    DateTimeOffset CreatedUtc);

public interface ILeaveStatusNotificationStore
{
    Task AddAsync(
        string mobile,
        string applicationReference,
        string newStatus,
        string? note,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingLeaveStatusNotification>> GetPendingAsync(
        string mobile,
        CancellationToken cancellationToken = default);

    Task MarkDeliveredAsync(
        string mobile,
        IReadOnlyCollection<string> notificationIds,
        CancellationToken cancellationToken = default);
}
