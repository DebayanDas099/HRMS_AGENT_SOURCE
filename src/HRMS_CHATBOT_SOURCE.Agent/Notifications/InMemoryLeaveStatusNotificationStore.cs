using System.Collections.Concurrent;

namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

/// <summary>
/// Fallback used when Cosmos is not configured (local/dev). Does not survive a
/// restart and is not safe across multiple app instances - same limitation as the
/// other in-memory stores in this codebase, for the same reason.
/// </summary>
public sealed class InMemoryLeaveStatusNotificationStore : ILeaveStatusNotificationStore
{
    private sealed record Entry(string Id, string ApplicationReference, string NewStatus, string? Note, DateTimeOffset CreatedUtc)
    {
        public DateTimeOffset? DeliveredUtc { get; set; }
    }

    private readonly ConcurrentDictionary<string, List<Entry>> _byMobile = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(
        string mobile,
        string applicationReference,
        string newStatus,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var list = _byMobile.GetOrAdd(mobile, _ => []);
        lock (list)
        {
            list.Add(new Entry(Guid.NewGuid().ToString("N"), applicationReference, newStatus, note, DateTimeOffset.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PendingLeaveStatusNotification>> GetPendingAsync(
        string mobile,
        CancellationToken cancellationToken = default)
    {
        if (!_byMobile.TryGetValue(mobile, out var list))
        {
            return Task.FromResult<IReadOnlyList<PendingLeaveStatusNotification>>([]);
        }

        IReadOnlyList<PendingLeaveStatusNotification> pending;
        lock (list)
        {
            pending = list
                .Where(e => e.DeliveredUtc is null)
                .OrderBy(e => e.CreatedUtc)
                .Select(e => new PendingLeaveStatusNotification(e.Id, e.ApplicationReference, e.NewStatus, e.Note, e.CreatedUtc))
                .ToList();
        }

        return Task.FromResult(pending);
    }

    public Task MarkDeliveredAsync(
        string mobile,
        IReadOnlyCollection<string> notificationIds,
        CancellationToken cancellationToken = default)
    {
        if (!_byMobile.TryGetValue(mobile, out var list))
        {
            return Task.CompletedTask;
        }

        lock (list)
        {
            var deliveredUtc = DateTimeOffset.UtcNow;
            foreach (var entry in list.Where(e => notificationIds.Contains(e.Id)))
            {
                entry.DeliveredUtc = deliveredUtc;
            }
        }

        return Task.CompletedTask;
    }
}
