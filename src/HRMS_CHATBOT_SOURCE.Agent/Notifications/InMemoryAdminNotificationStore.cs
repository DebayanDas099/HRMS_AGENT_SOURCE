using System.Collections.Concurrent;

namespace HRMS_CHATBOT_SOURCE.Agent.Notifications;

/// <summary>
/// Fallback used when Cosmos is not configured (local/dev). Does not survive a
/// restart and is not safe across multiple app instances - same limitation as the
/// other in-memory stores in this codebase, for the same reason.
/// </summary>
public sealed class InMemoryAdminNotificationStore : IAdminNotificationStore
{
    private sealed record Entry(string? ApplicationReference, string EmployeeMobile, string? EmployeeName, DateTimeOffset CreatedUtc)
    {
        public DateTimeOffset? ReadUtc { get; set; }
    }

    private readonly List<Entry> _entries = [];
    private readonly object _lock = new();

    public Task AddAsync(
        string? applicationReference,
        string employeeMobile,
        string? employeeName,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _entries.Add(new Entry(applicationReference, employeeMobile, employeeName, DateTimeOffset.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_entries.Count(e => e.ReadUtc is null));
        }
    }

    public Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var readUtc = DateTimeOffset.UtcNow;
            foreach (var entry in _entries.Where(e => e.ReadUtc is null))
            {
                entry.ReadUtc = readUtc;
            }
        }

        return Task.CompletedTask;
    }
}
