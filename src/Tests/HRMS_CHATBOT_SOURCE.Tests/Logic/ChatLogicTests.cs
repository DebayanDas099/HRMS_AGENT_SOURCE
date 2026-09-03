using System.ComponentModel.DataAnnotations;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using HRMS_CHATBOT_SOURCE.Logic;
using HRMS_CHATBOT_SOURCE.Repo.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Tests.Logic;

public class ChatLogicTests
{
    [Fact]
    public async Task SendMessageAsync_NoAgentsEnabled_ReturnsFixedRefusalWithoutRunningTheWorkflow()
    {
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

    [Fact]
    public async Task SendMessageAsync_PrefersRequestMobileOverToken()
    {
        var (chatLogic, access, runtime) = Build(
            enabledAgents: ["SupervisorAgent"],
            currentUser: new CurrentUserContext { Mobile = "9999888877" });

        await chatLogic.SendMessageAsync(Request("1234567890", "Apply leave"));

        Assert.Equal("1234567890", access.LastMobile);
        Assert.Equal("1234567890", runtime.AuthenticatedMobile);
    }

    [Fact]
    public async Task SendMessageAsync_FallsBackToTokenMobile_WhenRequestBodyOmitted()
    {
        var (chatLogic, access, runtime) = Build(
            enabledAgents: ["SupervisorAgent"],
            currentUser: new CurrentUserContext { Mobile = "9999888877" });

        await chatLogic.SendMessageAsync(new ChatTurnRequest { Message = "Apply leave" });

        Assert.Equal("9999888877", access.LastMobile);
        Assert.Equal("9999888877", runtime.AuthenticatedMobile);
    }

    [Fact]
    public async Task SendMessageAsync_FallsBackToRequestMobile_WhenNoAuthenticatedUser()
    {
        var (chatLogic, access, runtime) = Build(enabledAgents: ["SupervisorAgent"]);

        await chatLogic.SendMessageAsync(Request("1234567890", "Apply leave"));

        Assert.Equal("1234567890", access.LastMobile);
        Assert.Equal("1234567890", runtime.AuthenticatedMobile);
    }

    [Fact]
    public async Task SendMessageAsync_VerifiedAdmin_AddsLeaveApprovalAgentToTheEnabledList()
    {
        var (chatLogic, _, runtime) = Build(
            enabledAgents: ["SupervisorAgent", "KnowledgeAgent"],
            currentUser: new CurrentUserContext { Mobile = "9999888877", IsAdmin = "Y" });

        var response = await chatLogic.SendMessageAsync(Request("9999999999", "hello"));

        Assert.Contains("LeaveApprovalAgent", response.EnabledAgents, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("KnowledgeAgent", response.EnabledAgents, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessageAsync_NonAdmin_NeverGetsLeaveApprovalAgent()
    {
        var (chatLogic, _, runtime) = Build(
            enabledAgents: ["SupervisorAgent", "KnowledgeAgent"],
            currentUser: new CurrentUserContext { Mobile = "9999888877", IsAdmin = "N" });

        var response = await chatLogic.SendMessageAsync(Request("9999999999", "hello"));

        Assert.DoesNotContain("LeaveApprovalAgent", response.EnabledAgents, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessageAsync_AnonymousCaller_NeverGetsLeaveApprovalAgent()
    {
        // No CurrentUser at all - the public, unauthenticated chat page shape.
        var (chatLogic, _, runtime) = Build(enabledAgents: ["SupervisorAgent", "KnowledgeAgent"]);

        var response = await chatLogic.SendMessageAsync(Request("9999999999", "hello"));

        Assert.DoesNotContain("LeaveApprovalAgent", response.EnabledAgents, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessageAsync_VerifiedAdmin_WithNoEmployeeAccess_StillRunsForLeaveApprovalOnly()
    {
        // The mobile typed into the admin test-chat box has no employee-side group
        // access at all - the admin capability must not depend on that.
        var (chatLogic, _, runtime) = Build(
            enabledAgents: [],
            currentUser: new CurrentUserContext { Mobile = "9999888877", IsAdmin = "Y" });

        var response = await chatLogic.SendMessageAsync(Request("0000000000", "list pending approvals"));

        Assert.True(runtime.WasCalled);
        Assert.Equal(["LeaveApprovalAgent"], response.EnabledAgents);
        Assert.NotEqual(
            "Your mobile number is not registered for this service. Please contact HR/IT support.",
            response.Reply);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessageAsync_MissingMobile_ThrowsBeforeCallingAnything(string? mobile)
    {
        var (chatLogic, access, runtime) = Build(enabledAgents: []);

        await Assert.ThrowsAsync<ValidationException>(
            () => chatLogic.SendMessageAsync(Request(mobile!, "hello")));

        Assert.False(access.WasCalled);
        Assert.False(runtime.WasCalled);
    }

    [Fact]
    public async Task SendMessageAsync_MissingMessage_ThrowsBeforeCallingAnything()
    {
        var (chatLogic, access, runtime) = Build(enabledAgents: []);

        await Assert.ThrowsAsync<ValidationException>(
            () => chatLogic.SendMessageAsync(Request("9999999999", "   ")));

        Assert.False(access.WasCalled);
        Assert.False(runtime.WasCalled);
    }

    private static ChatTurnRequest Request(string mobile, string message, string? conversationId = null)
    {
        return new ChatTurnRequest { Mobile = mobile, Message = message, ConversationId = conversationId };
    }

    private static (ChatLogic ChatLogic, SpyAgentAccessService Access, SpyChatRuntime Runtime) Build(
        IReadOnlyList<string> enabledAgents,
        CurrentUserContext? currentUser = null,
        bool voiceEnabled = false)
    {
        var access = new SpyAgentAccessService(enabledAgents, voiceEnabled);
        var runtime = new SpyChatRuntime();
        var serviceContext = new FakeServiceContext { CurrentUser = currentUser };
        var chatLogic = new ChatLogic(
            access,
            runtime,
            new StubUserProfileRepo(),
            new StubSpeechTranslationService(),
            serviceContext,
            Options.Create(new AzureSpeechSettings()),
            NullLogger<ChatLogic>.Instance);
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

        public Task<string?> GetUserMobileByUserIdAsync(string? userId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string?> GetUserEmailByMobileAsync(string? mobile, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    private sealed class FakeServiceContext : IServiceContext
    {
        public Microsoft.Extensions.Configuration.IConfiguration Configuration { get; } =
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        public Microsoft.AspNetCore.Hosting.IWebHostEnvironment CurrentEnvironment { get; } = null!;
        public Microsoft.AspNetCore.Http.HttpContext? RequestContext { get; set; }
        public Microsoft.Extensions.Caching.Memory.IMemoryCache MemoryCache { get; } = null!;
        public bool IsProduction => false;
        public int RequestTimeout => 60;
        public string ContentRootPath { get; } = string.Empty;
        public string? RequestTraceId => null;
        public CurrentUserContext? CurrentUser { get; set; }
        public string? IpAddress => null;
        public string? HostUrl => null;
        public MCC.Foundation.MSSQLHelper.Models.MSSQLConnectionModel SQLConnectionModel { get; } = null!;
    }

    [Fact]
    public async Task SendMessageStreamAsync_NoAgentsEnabled_YieldsAccessDeniedDoneChunk()
    {
        var (chatLogic, _, runtime) = Build(enabledAgents: []);

        var chunks = await CollectStream(chatLogic.SendMessageStreamAsync(Request("9999999999", "hello")));

        Assert.Single(chunks);
        Assert.Equal("done", chunks[0].Type);
        Assert.Contains("not registered", chunks[0].Reply, StringComparison.OrdinalIgnoreCase);
        Assert.False(runtime.WasStreamCalled);
    }

    [Fact]
    public async Task SendMessageStreamAsync_SomeAgentsEnabled_StreamsFromRuntime()
    {
        var (chatLogic, _, runtime) = Build(enabledAgents: ["SupervisorAgent", "KnowledgeAgent"]);

        var chunks = await CollectStream(chatLogic.SendMessageStreamAsync(Request("9999999999", "hello")));

        Assert.True(runtime.WasStreamCalled);
        Assert.Contains(chunks, c => c.Type == "delta");
        Assert.Contains(chunks, c => c.Type == "done" && c.Reply == "stub reply");
    }

    [Fact]
    public async Task GetVoiceInputEnabledAsync_WhenDisabled_ReturnsDisabledMessage()
    {
        var (chatLogic, _, _) = Build(enabledAgents: ["SupervisorAgent"], voiceEnabled: false);

        var response = await chatLogic.GetVoiceInputEnabledAsync("9999999999");

        Assert.False(response.VoiceEnabled);
        Assert.Equal(VoiceInputEnabledResponse.DefaultDisabledMessage, response.DisabledMessage);
    }

    [Fact]
    public async Task GetVoiceInputEnabledAsync_WhenEnabled_ReturnsTrue()
    {
        var (chatLogic, _, _) = Build(enabledAgents: ["SupervisorAgent"], voiceEnabled: true);

        var response = await chatLogic.GetVoiceInputEnabledAsync("9999999999");

        Assert.True(response.VoiceEnabled);
    }

    [Fact]
    public async Task TranscribeVoiceAsync_WhenVoiceDisabled_Throws()
    {
        var (chatLogic, _, _) = Build(enabledAgents: ["SupervisorAgent"], voiceEnabled: false);
        var file = CreateTestAudioFile();

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => chatLogic.TranscribeVoiceAsync("9999999999", file));

        Assert.Equal(VoiceInputEnabledResponse.DefaultDisabledMessage, ex.Message);
    }

    [Fact]
    public async Task TranscribeVoiceAsync_WhenVoiceEnabled_ReturnsEnglishText()
    {
        var (chatLogic, _, _) = Build(enabledAgents: ["SupervisorAgent"], voiceEnabled: true);
        var file = CreateTestAudioFile();

        var response = await chatLogic.TranscribeVoiceAsync("9999999999", file);

        Assert.Equal("stub english", response.EnglishText);
    }

    private static FormFile CreateTestAudioFile()
    {
        return new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "audio", "voice.wav")
        {
            Headers = new HeaderDictionary(),
            ContentType = "audio/wav"
        };
    }

    private static async Task<List<ChatStreamChunk>> CollectStream(
        IAsyncEnumerable<ChatStreamChunk> stream)
    {
        var chunks = new List<ChatStreamChunk>();
        await foreach (var chunk in stream)
        {
            chunks.Add(chunk);
        }

        return chunks;
    }

    private sealed class StubSpeechTranslationService : ISpeechTranslationService
    {
        public Task<VoiceTranscriptionResponse> TranscribeAndTranslateToEnglishAsync(
            Stream audioStream,
            string contentType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new VoiceTranscriptionResponse { EnglishText = "stub english" });
    }

    private sealed class SpyAgentAccessService(IReadOnlyList<string> result, bool voiceEnabled) : IAgentAccessService
    {
        public bool WasCalled { get; private set; }
        public string? LastMobile { get; private set; }

        public Task<IReadOnlyList<string>> GetEnabledAgentNamesAsync(
            string? mobile, CancellationToken cancellationToken = default) =>
            GetWorkflowAgentNamesAsync(mobile, cancellationToken);

        public Task<IReadOnlyList<string>> GetWorkflowAgentNamesAsync(
            string? mobile, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastMobile = mobile;
            return Task.FromResult<IReadOnlyList<string>>(result);
        }

        public Task<bool> IsVoiceInputEnabledAsync(
            string mobile, CancellationToken cancellationToken = default) =>
            Task.FromResult(voiceEnabled);
    }

    private sealed class SpyChatRuntime : IHrmsChatRuntime
    {
        public bool WasCalled { get; private set; }
        public bool WasStreamCalled { get; private set; }
        public string? AuthenticatedMobile { get; private set; }

        public Task<ChatTurnResponse> RunAsync(
            IReadOnlyCollection<string> enabledAgentNames,
            string? conversationId,
            string message,
            string? authenticatedMobile = null,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            AuthenticatedMobile = authenticatedMobile;
            return Task.FromResult(new ChatTurnResponse
            {
                ConversationId = conversationId ?? "generated",
                Reply = "stub reply",
                EnabledAgents = [.. enabledAgentNames],
                LastSpeaker = "SupervisorAgent"
            });
        }

        public async IAsyncEnumerable<ChatStreamChunk> RunStreamAsync(
            IReadOnlyCollection<string> enabledAgentNames,
            string? conversationId,
            string message,
            string? authenticatedMobile = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            WasStreamCalled = true;
            WasCalled = true;
            AuthenticatedMobile = authenticatedMobile;
            yield return ChatStreamChunk.Delta("stub ");
            yield return ChatStreamChunk.Delta("reply");
            yield return ChatStreamChunk.Done(
                conversationId ?? "generated",
                "stub reply",
                [.. enabledAgentNames],
                "SupervisorAgent");
        }
    }
}
