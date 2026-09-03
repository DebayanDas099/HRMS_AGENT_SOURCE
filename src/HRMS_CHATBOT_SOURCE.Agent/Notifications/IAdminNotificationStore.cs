namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

public interface IAdminNotificationStore
{
    Task AddAsync(
        string? applicationReference,
        string employeeMobile,
        string? employeeName,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}
