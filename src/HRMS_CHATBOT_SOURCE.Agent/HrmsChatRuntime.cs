using System.Collections.Concurrent;
using HRMS_CHATBOT_SOURCE.Agent.Skills;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using MCC.Foundation.Guardrails;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent;

public sealed class HrmsChatRuntime : IHrmsChatRuntime
{
    private const string GuardrailBlockedReply =
        "I can't help with that request. If you need assistance, please contact HR directly.";

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

    public async Task<ChatTurnResponse> RunAsync(
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
        var turnMessages = ApplyCurrentAccessOverride(snapshot, enabled);
        var reply = await RunTurnAsync(workflow, turnMessages, cancellationToken).ConfigureAwait(false);

        lock (history)
        {
            history.Add(new ChatMessage(ChatRole.Assistant, reply.Text));
        }

        return new ChatTurnResponse
        {
            ConversationId = id,
            Reply = reply.Text,
            EnabledAgents = enabled,
            LastSpeaker = reply.LastSpeaker
        };
    }

    /// <summary>
    /// Wraps turn execution so a guardrail block degrades into a safe reply instead of
    /// an unhandled exception. The shared IChatClient is wrapped with UseMccGuardrails,
    /// so every model call this turn makes - the routing turn, each specialist turn,
    /// and the tool-result round-trip after PolicyKnowledgeTools returns - is screened
    /// on both the way in and the way out; a violation throws from inside whichever
    /// call tripped it, which can be anywhere in ExecuteTurnAsync.
    /// </summary>
    private async Task<(string Text, string? LastSpeaker)> RunTurnAsync(
        Workflow workflow,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteTurnAsync(workflow, messages, cancellationToken).ConfigureAwait(false);
        }
        catch (GuardrailViolationException ex)
        {
            _logger.LogWarning(
                "Guardrail blocked a chat turn: provider={Provider} severity={Severity} reason={Reason}",
                ex.Provider,
                ex.Severity,
                ex.Reason);

            return (GuardrailBlockedReply, null);
        }
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

    internal static IReadOnlyList<ChatMessage> ApplyCurrentAccessOverride(
        IReadOnlyList<ChatMessage> history,
        IReadOnlyCollection<string> enabledAgentNames)
    {
        var notice = new ChatMessage(
            ChatRole.System,
            SupervisorSkillComposer.BuildCurrentAccessNotice(enabledAgentNames));

        if (history.Count == 0)
        {
            return [notice];
        }

        var turnMessages = new List<ChatMessage>(history.Count + 1);
        turnMessages.AddRange(history.Take(history.Count - 1));
        turnMessages.Add(notice);
        turnMessages.Add(history[^1]);
        return turnMessages;
    }
}
