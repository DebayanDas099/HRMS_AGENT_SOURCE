using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Domain.Interfaces;

/// <summary>
/// Runs one turn of the Supervisor handoff workflow. Declared here, not in the
/// Agent project, so Logic can depend on it without referencing Agent directly
/// - the same split used for <see cref="IDocumentIngestionPipeline"/>.
/// </summary>
public interface IHrmsChatRuntime
{
    Task<ChatTurnResponse> RunAsync(
        IReadOnlyCollection<string> enabledAgentNames,
        string? conversationId,
        string message,
        string? authenticatedMobile = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamChunk> RunStreamAsync(
        IReadOnlyCollection<string> enabledAgentNames,
        string? conversationId,
        string message,
        string? authenticatedMobile = null,
        CancellationToken cancellationToken = default);
}
