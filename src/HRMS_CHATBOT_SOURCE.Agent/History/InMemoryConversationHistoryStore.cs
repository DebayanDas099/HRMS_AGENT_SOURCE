using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace HRMS_CHATBOT_SOURCE.Agent.History;

/// <summary>
/// Fallback used when Cosmos is not configured (local/dev). Does not survive a restart
/// and is not safe across multiple app instances - the same limitation the in-memory
/// checkpoint manager has, and for the same reason: it keeps local/dev usable without
/// requiring Cosmos.
/// </summary>
public sealed class InMemoryConversationHistoryStore : IConversationHistoryStore
{
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _history = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var messages = _history.TryGetValue(conversationId, out var existing)
            ? (IReadOnlyList<ChatMessage>)[.. existing]
            : [];

        return Task.FromResult(messages);
    }

    public Task SaveHistoryAsync(string conversationId, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        _history[conversationId] = [.. messages];
        return Task.CompletedTask;
    }
}
