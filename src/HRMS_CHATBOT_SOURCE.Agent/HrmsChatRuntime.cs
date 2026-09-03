using HRMS_CHATBOT_SOURCE.Agent.History;
using HRMS_CHATBOT_SOURCE.Agent.Notifications;
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
    private readonly IConversationHistoryStore _historyStore;
    private readonly ILeaveStatusNotificationStore _leaveStatusNotificationStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HrmsChatRuntime> _logger;

    public HrmsChatRuntime(
        HrmsHandoffWorkflowFactory workflowFactory,
        CheckpointManager checkpointManager,
        IConversationHistoryStore historyStore,
        ILeaveStatusNotificationStore leaveStatusNotificationStore,
        IServiceScopeFactory scopeFactory,
        ILogger<HrmsChatRuntime> logger)
    {
        _workflowFactory = workflowFactory;
        _checkpointManager = checkpointManager;
        _historyStore = historyStore;
        _leaveStatusNotificationStore = leaveStatusNotificationStore;
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
        ChatTurnResponse? result = null;

        await foreach (var chunk in RunStreamAsync(
            enabledAgentNames,
            conversationId,
            message,
            authenticatedMobile,
            cancellationToken).ConfigureAwait(false))
        {
            if (string.Equals(chunk.Type, "done", StringComparison.OrdinalIgnoreCase))
            {
                result = new ChatTurnResponse
                {
                    ConversationId = chunk.ConversationId ?? string.Empty,
                    Reply = chunk.Reply ?? string.Empty,
                    EnabledAgents = chunk.EnabledAgents ?? [],
                    LastSpeaker = chunk.LastSpeaker
                };
            }
            else if (string.Equals(chunk.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(chunk.Message ?? "Chat stream failed.");
            }
        }

        return result ?? throw new InvalidOperationException("Chat stream ended without a done chunk.");
    }

    public async IAsyncEnumerable<ChatStreamChunk> RunStreamAsync(
        IReadOnlyCollection<string> enabledAgentNames,
        string? conversationId,
        string message,
        string? authenticatedMobile = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new System.ComponentModel.DataAnnotations.ValidationException("Message is required.");
        }

        var id = string.IsNullOrWhiteSpace(conversationId)
            ? Guid.NewGuid().ToString("N")
            : conversationId.Trim();

        var existingHistory = await _historyStore.GetHistoryAsync(id, cancellationToken).ConfigureAwait(false);
        var history = new List<ChatMessage>(existingHistory)
        {
            new ChatMessage(ChatRole.User, message.Trim())
        };

        var enabled = enabledAgentNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var workflow = _workflowFactory.Build(enabled);
        using var scope = _scopeFactory.CreateScope();
        var commonLogic = scope.ServiceProvider.GetRequiredService<ICommonLogic>();
        var turnMessages = ApplyCurrentAccessOverride(history, enabled, commonLogic, authenticatedMobile);

        var noticePrefix = await GetPendingLeaveNoticePrefixAsync(authenticatedMobile, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrEmpty(noticePrefix))
        {
            yield return ChatStreamChunk.Delta(noticePrefix);
        }

        var agentReplyBuilder = new System.Text.StringBuilder();
        string? lastSpeaker = null;

        await foreach (var delta in StreamTurnAsync(workflow, turnMessages, id, cancellationToken).ConfigureAwait(false))
        {
            if (delta.IsGuardrailBlock)
            {
                agentReplyBuilder.Clear();
                agentReplyBuilder.Append(GuardrailBlockedReply);
                lastSpeaker = null;
                yield return ChatStreamChunk.Delta(GuardrailBlockedReply);
                break;
            }

            if (!string.IsNullOrEmpty(delta.Text))
            {
                agentReplyBuilder.Append(delta.Text);
                lastSpeaker = delta.LastSpeaker ?? lastSpeaker;
                yield return ChatStreamChunk.Delta(delta.Text);
            }
        }

        var agentReply = agentReplyBuilder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(agentReply))
        {
            _logger.LogWarning("Handoff workflow produced an empty reply.");
            agentReply = "I could not produce a response. Please try again.";
            yield return ChatStreamChunk.Delta(agentReply);
        }

        var fullReply = string.IsNullOrEmpty(noticePrefix)
            ? agentReply
            : noticePrefix + agentReply;

        history.Add(new ChatMessage(ChatRole.Assistant, fullReply));
        await _historyStore.SaveHistoryAsync(id, history, cancellationToken).ConfigureAwait(false);

        yield return ChatStreamChunk.Done(id, fullReply, enabled, lastSpeaker);
    }

    private async Task<string?> GetPendingLeaveNoticePrefixAsync(
        string? authenticatedMobile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(authenticatedMobile))
        {
            return null;
        }

        try
        {
            var pending = await _leaveStatusNotificationStore
                .GetPendingAsync(authenticatedMobile, cancellationToken)
                .ConfigureAwait(false);

            if (pending.Count == 0)
            {
                return null;
            }

            var notices = pending.Select(n => $"Your leave request ({n.ApplicationReference}) was {n.NewStatus}."
                + (string.IsNullOrWhiteSpace(n.Note) ? string.Empty : $" Note: {n.Note}"));

            await _leaveStatusNotificationStore
                .MarkDeliveredAsync(authenticatedMobile, pending.Select(n => n.Id).ToList(), cancellationToken)
                .ConfigureAwait(false);

            return string.Join(Environment.NewLine, notices) + Environment.NewLine + Environment.NewLine;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not check pending leave notifications for {Mobile}.", authenticatedMobile);
            return null;
        }
    }

    private async IAsyncEnumerable<(string? Text, string? LastSpeaker, bool IsGuardrailBlock)> StreamTurnAsync(
        Workflow workflow,
        IReadOnlyList<ChatMessage> messages,
        string sessionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in ExecuteTurnEventStreamAsync(workflow, messages, sessionId, cancellationToken)
            .ConfigureAwait(false))
        {
            switch (evt)
            {
                case GuardrailBlockedMarker:
                    yield return (null, null, true);
                    yield break;
                case TurnDelta delta:
                    yield return (delta.Text, delta.LastSpeaker, false);
                    break;
            }
        }
    }

    private async IAsyncEnumerable<object> ExecuteTurnEventStreamAsync(
        Workflow workflow,
        IReadOnlyList<ChatMessage> messages,
        string sessionId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var run = await InProcessExecution
            .RunStreamingAsync(workflow, messages, _checkpointManager, sessionId, cancellationToken)
            .ConfigureAwait(false);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true)).ConfigureAwait(false);

        var text = new System.Text.StringBuilder();

        await foreach (var evt in run.WatchStreamAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (evt is ExecutorFailedEvent failed)
            {
                if (GuardrailViolationException.TryUnwrap(failed.Data, out var blocked))
                {
                    yield return new GuardrailBlockedMarker();
                    yield break;
                }

                throw failed.Data ?? new InvalidOperationException($"Executor '{failed.ExecutorId}' failed.");
            }

            if (evt is WorkflowErrorEvent workflowError)
            {
                if (GuardrailViolationException.TryUnwrap(workflowError.Exception, out var blocked))
                {
                    yield return new GuardrailBlockedMarker();
                    yield break;
                }

                throw workflowError.Exception ?? new InvalidOperationException("Workflow error.");
            }

            if (evt is AgentResponseUpdateEvent update)
            {
                if (!string.IsNullOrEmpty(update.Update.Text))
                {
                    text.Append(update.Update.Text);
                    yield return new TurnDelta(update.Update.Text, update.ExecutorId);
                }
            }
            else if (evt is WorkflowOutputEvent output)
            {
                if (output.As<List<ChatMessage>>() is { Count: > 0 } outputMessages)
                {
                    var lastAssistant = outputMessages.LastOrDefault(message => message.Role == ChatRole.Assistant);
                    if (lastAssistant != null && text.Length == 0 && !string.IsNullOrEmpty(lastAssistant.Text))
                    {
                        text.Append(lastAssistant.Text);
                        yield return new TurnDelta(lastAssistant.Text, null);
                    }
                }

                break;
            }
        }

        await LogCheckpointAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    private sealed record TurnDelta(string? Text, string? LastSpeaker);

    private sealed class GuardrailBlockedMarker;

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
