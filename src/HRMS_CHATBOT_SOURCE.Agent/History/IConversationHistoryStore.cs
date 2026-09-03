using Microsoft.Extensions.AI;

namespace HRMS_CHATBOT_SOURCE.Agent.History;

/// <summary>
/// Persists the per-conversation transcript replayed as each turn's input. Externalising
/// this (in place of an in-process dictionary) is what lets API instances be stateless:
/// any instance can serve any turn of a conversation, and a restart does not lose history
/// mid-conversation.
/// </summary>
public interface IConversationHistoryStore
{
    Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(string conversationId, CancellationToken cancellationToken = default);

    Task SaveHistoryAsync(string conversationId, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
