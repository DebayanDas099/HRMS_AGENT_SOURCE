using System.Collections.Concurrent;
using HRMS_CHATBOT_SOURCE.Agent.Skills;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Logic.Common;
using MCC.Foundation.Guardrails;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRMS_CHATBOT_SOURCE.Agent;

public sealed class HrmsChatRuntime : IHrmsChatRuntime
{
    private const string GuardrailBlockedReply =
        "I can't help with that request. If you need assistance, please contact HR directly.";

    private readonly HrmsHandoffWorkflowFactory _workflowFactory;
    private readonly CheckpointManager _checkpointManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HrmsChatRuntime> _logger;

    /// <summary>
    /// The transcript replayed as this turn's input. This is distinct from the MAF
    /// checkpoint recorded per run below, and stays in-process for now: MAF's own
    /// resumption path (ResumeStreamingAsync) is documented as resuming a run
    /// suspended on a RequestPort's RequestInfoEvent, and this workflow has no
    /// RequestPort - every turn runs the handoff graph to completion. Without a
    /// suspend point there is nothing for a checkpoint to resume, so it cannot yet
    /// replace this cache; it becomes load-bearing the moment a RequestPort (e.g.
    /// the leave confirmation gate) is added.
    /// </summary>
    private readonly ConcurrentDictionary<string, List<ChatMessage>> _conversationHistory = new(StringComparer.OrdinalIgnoreCase);

    public HrmsChatRuntime(
        HrmsHandoffWorkflowFactory workflowFactory,
        CheckpointManager checkpointManager,
        IServiceScopeFactory scopeFactory,
        ILogger<HrmsChatRuntime> logger)
    {
        _workflowFactory = workflowFactory;
        _checkpointManager = checkpointManager;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<ChatTurnResponse> RunAsync(
        IReadOnlyCollection<string> enabledAgentNames,
        string? conversationId,
        string message,
        string? authenticatedMobile = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("Message is required.");
        }

        var id = string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId.Trim();

        var history = _conversationHistory.GetOrAdd(id, _ => []);
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
        using var scope = _scopeFactory.CreateScope();
        var commonLogic = scope.ServiceProvider.GetRequiredService<ICommonLogic>();
        var turnMessages = ApplyCurrentAccessOverride(snapshot, enabled, commonLogic, authenticatedMobile);
        var reply = await RunTurnAsync(workflow, turnMessages, id, cancellationToken).ConfigureAwait(false);

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
        string sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteTurnAsync(workflow, messages, sessionId, cancellationToken).ConfigureAwait(false);
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
        string sessionId,
        CancellationToken cancellationToken)
    {
        // Passing the checkpoint manager and a stable session id (the conversation id)
        // makes this a genuine MAF-tracked run rather than a session-less one-off call:
        // MAF externalises a real, queryable checkpoint per run under this session,
        // which is the audit trail SP10 calls for and the exact seam ResumeStreamingAsync
        // will need once a RequestPort exists.
        await using var run = await InProcessExecution
            .RunStreamingAsync(workflow, messages, _checkpointManager, sessionId, cancellationToken)
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

        await LogCheckpointAsync(sessionId, cancellationToken).ConfigureAwait(false);

        var reply = text.ToString().Trim();
        if (string.IsNullOrWhiteSpace(reply))
        {
            _logger.LogWarning("Handoff workflow produced an empty reply.");
            reply = "I could not produce a response. Please try again.";
        }

        return (reply, lastSpeaker);
    }

    private async Task LogCheckpointAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var checkpoint = await _checkpointManager
                .GetLatestCheckpointAsync(sessionId, cancellationToken)
                .ConfigureAwait(false);

            if (checkpoint != null)
            {
                _logger.LogInformation(
                    "Checkpointed conversation {SessionId} at {CheckpointId}.",
                    checkpoint.SessionId,
                    checkpoint.CheckpointId);
            }
        }
        catch (Exception ex)
        {
            // Observability only - a checkpoint lookup failure must not fail the turn.
            _logger.LogDebug(ex, "Could not read the latest checkpoint for conversation {SessionId}.", sessionId);
        }
    }

    internal IReadOnlyList<ChatMessage> ApplyCurrentAccessOverride(
        IReadOnlyList<ChatMessage> history,
        IReadOnlyCollection<string> enabledAgentNames,
        ICommonLogic commonLogic,
        string? authenticatedMobile = null)
    {
        var noticeText = SupervisorSkillComposer.BuildCurrentAccessNotice(enabledAgentNames);

        if (!string.IsNullOrWhiteSpace(authenticatedMobile))
        {
            noticeText += Environment.NewLine + Environment.NewLine
                + SupervisorSkillComposer.BuildAuthenticatedEmployeeNotice(authenticatedMobile);
        }

        var latestUserMessage = history.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        var parsedDateNotice = SupervisorSkillComposer.BuildParsedDateNotice(latestUserMessage, commonLogic);
        if (!string.IsNullOrWhiteSpace(parsedDateNotice))
        {
            noticeText += Environment.NewLine + Environment.NewLine + parsedDateNotice;
        }

        var notice = new ChatMessage(ChatRole.System, noticeText);

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
