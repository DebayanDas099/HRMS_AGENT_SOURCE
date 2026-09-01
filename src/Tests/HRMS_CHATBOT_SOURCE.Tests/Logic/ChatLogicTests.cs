using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.Logic;
using HRMS_CHATBOT_SOURCE.Repo.Admin;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRMS_CHATBOT_SOURCE.Tests.Logic;

public class ChatLogicTests
{
    [Fact]
    public async Task SendMessageAsync_NoAgentsEnabled_ReturnsFixedRefusalWithoutRunningTheWorkflow()
    {
        // No agents enabled covers both an unregistered number and a registered number
        // with nothing turned on for it - either way access must be denied identically,
        // and the workflow must never run: HandoffWorkflowTemplate force-adds Supervisor
        // to whatever set it is given, so only skipping the run entirely is safe.
        var (chatLogic, access, runtime) = Build(enabledAgents: []);

        var response = await chatLogic.SendMessageAsync(Request("9999999999", "hello"));

        Assert.Equal(
            "Your mobile number is not registered for this service. Please contact HR/IT support.",
            response.Reply);
        Assert.Empty(response.EnabledAgents);
        Assert.Null(response.LastSpeaker);
        Assert.False(runtime.WasCalled);
    }

    [Fact]
    public async Task SendMessageAsync_NoAgentsEnabled_StillReturnsAUsableConversationId()
    {
        var (chatLogic, _, _) = Build(enabledAgents: []);

        var withoutId = await chatLogic.SendMessageAsync(Request("9999999999", "hello", conversationId: null));
        var withId = await chatLogic.SendMessageAsync(Request("9999999999", "hello", conversationId: "existing-thread"));

        Assert.False(string.IsNullOrWhiteSpace(withoutId.ConversationId));
        Assert.Equal("existing-thread", withId.ConversationId);
    }

    [Fact]
    public async Task SendMessageAsync_SomeAgentsEnabled_RunsTheWorkflow()
    {
        var (chatLogic, _, runtime) = Build(enabledAgents: ["SupervisorAgent", "KnowledgeAgent"]);

        var response = await chatLogic.SendMessageAsync(Request("9999999999", "hello"));

        Assert.True(runtime.WasCalled);
        Assert.Equal("stub reply", response.Reply);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessageAsync_MissingMobile_ThrowsBeforeCallingAnything(string? mobile)
    {
        var (chatLogic, access, runtime) = Build(enabledAgents: []);

        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => chatLogic.SendMessageAsync(Request(mobile!, "hello")));

        Assert.False(access.WasCalled);
        Assert.False(runtime.WasCalled);
    }

    [Fact]
    public async Task SendMessageAsync_MissingMessage_ThrowsBeforeCallingAnything()
    {
        var (chatLogic, access, runtime) = Build(enabledAgents: []);

        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(
            () => chatLogic.SendMessageAsync(Request("9999999999", "   ")));

        Assert.False(access.WasCalled);
        Assert.False(runtime.WasCalled);
    }

    private static ChatTurnRequest Request(string mobile, string message, string? conversationId = null)
    {
        return new ChatTurnRequest { Mobile = mobile, Message = message, ConversationId = conversationId };
    }

    private static (ChatLogic ChatLogic, SpyAgentAccessService Access, SpyChatRuntime Runtime) Build(
        IReadOnlyList<string> enabledAgents)
    {
        var access = new SpyAgentAccessService(enabledAgents);
        var runtime = new SpyChatRuntime();
        var chatLogic = new ChatLogic(access, runtime, new StubUserProfileRepo(), NullLogger<ChatLogic>.Instance);
        return (chatLogic, access, runtime);
    }

    private sealed class StubUserProfileRepo : IUserProfileRepo
    {
        public Task<MSSQLResponse?> ValidateAdminLoginAsync(LoginRequest? request, CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(null);

        public Task<MSSQLResponse?> UpdateLastAccessedAsync(string? userId, CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(null);

        public Task<MSSQLResponse?> GetActiveMobileNumbersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(null);
    }

    private sealed class SpyAgentAccessService(IReadOnlyList<string> result) : IAgentAccessService
    {
        public bool WasCalled { get; private set; }

        public Task<IReadOnlyList<string>> GetEnabledAgentNamesAsync(
            string? mobile, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(result);
        }
    }

    private sealed class SpyChatRuntime : IHrmsChatRuntime
    {
        public bool WasCalled { get; private set; }

        public Task<ChatTurnResponse> RunAsync(
            IReadOnlyCollection<string> enabledAgentNames,
            string? conversationId,
            string message,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new ChatTurnResponse
            {
                ConversationId = conversationId ?? "generated",
                Reply = "stub reply",
                EnabledAgents = [.. enabledAgentNames],
                LastSpeaker = "SupervisorAgent"
            });
        }
    }
}
