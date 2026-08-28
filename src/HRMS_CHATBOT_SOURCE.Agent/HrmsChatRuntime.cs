using System.Collections.Concurrent;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent;

public interface IHrmsChatRuntime
{
    Task<HrmsChatTurnResult> RunAsync(
        IReadOnlyCollection<string> enabledAgentNames,
        string? conversationId,
        string message,
        CancellationToken cancellationToken = default);
}

public sealed class HrmsChatTurnResult
{
    public required string ConversationId { get; init; }

    public required string Reply { get; init; }

    public required IReadOnlyList<string> EnabledAgents { get; init; }

    public string? LastSpeaker { get; init; }
}

public sealed class HrmsChatRuntime : IHrmsChatRuntime
{
    private readonly HrmsHandoffWorkflowFactory _workflowFactory;
    private readonly ILogger<HrmsChatRuntime> _logger;
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _sessions = new(StringComparer.OrdinalIgnoreCase);

    public HrmsChatRuntime(
        HrmsHandoffWorkflowFactory workflowFactory,
        ILogger<HrmsChatRuntime> logger)
    {
        _workflowFactory = workflowFactory;
        _logger = logger;
    }

    public async Task<HrmsChatTurnResult> RunAsync(
        IReadOnlyCollection<string> enabledAgentNames,
        string? conversationId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("Message is required.");
        }

        var id = string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId.Trim();

        var history = _sessions.GetOrAdd(id, _ => []);
        List<ChatMessage> snapshot;
        lock (history)
        {
            history.Add(new ChatMessage(ChatRole.User, message.Trim()));
            snapshot = [.. history];
        }

        var enabled = enabledAgentNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var workflow = _workflowFactory.Build(enabled);
        var reply = await ExecuteTurnAsync(workflow, snapshot, cancellationToken).ConfigureAwait(false);

        lock (history)
        {
            history.Add(new ChatMessage(ChatRole.Assistant, reply.Text));
        }

        return new HrmsChatTurnResult
        {
            ConversationId = id,
            Reply = reply.Text,
            EnabledAgents = enabled,
            LastSpeaker = reply.LastSpeaker
        };
    }

    private async Task<(string Text, string? LastSpeaker)> ExecuteTurnAsync(
        Workflow workflow,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        await using var run = await InProcessExecution.RunStreamingAsync(workflow, messages, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

        var text = new System.Text.StringBuilder();
        string? lastSpeaker = null;

        await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (evt is AgentResponseUpdateEvent update)
            {
                lastSpeaker = update.ExecutorId;
                if (!string.IsNullOrEmpty(update.Update.Text))
                {
                    text.Append(update.Update.Text);
                }
            }
            else if (evt is WorkflowOutputEvent output)
            {
                if (output.As<List<ChatMessage>>() is { Count: > 0 } outputMessages)
                {
                    var lastAssistant = outputMessages.LastOrDefault(message => message.Role == ChatRole.Assistant);
                    if (lastAssistant != null && text.Length == 0)
                    {
                        text.Append(lastAssistant.Text);
                    }
                }

                break;
            }
        }

        var reply = text.ToString().Trim();
        if (string.IsNullOrWhiteSpace(reply))
        {
            _logger.LogWarning("Handoff workflow produced an empty reply.");
            reply = "I could not produce a response. Please try again.";
        }

        return (reply, lastSpeaker);
    }
}
