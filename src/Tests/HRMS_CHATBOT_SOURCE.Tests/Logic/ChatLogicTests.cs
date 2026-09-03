using System.ComponentModel.DataAnnotations;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using HRMS_CHATBOT_SOURCE.Logic;
using HRMS_CHATBOT_SOURCE.Repo.Admin;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

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
    public async Task SendMessageAsync_VerifiedAdminWithExplicitHeader_AddsLeaveApprovalAgentToTheEnabledList()
    {
        var (chatLogic, _, runtime) = Build(
            enabledAgents: ["SupervisorAgent", "KnowledgeAgent"],
            currentUser: new CurrentUserContext { Mobile = "9999888877", IsAdmin = "Y" },
            hasExplicitAdminHeader: true);

        var response = await chatLogic.SendMessageAsync(Request("9999999999", "hello"));

        Assert.Contains("LeaveApprovalAgent", response.EnabledAgents, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("KnowledgeAgent", response.EnabledAgents, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessageAsync_AdminClaimFromCookieOnly_NeverGetsLeaveApprovalAgent()
    {
        // Regression test: an admin who is separately logged into /Admin in the same
        // browser carries an hrms_admin_token cookie on every same-origin request,
        // including a fetch() from the public, unauthenticated /chat page - which
        // never explicitly asked for or attached any admin credential. IsAdmin=Y
        // alone must not be enough; without an explicit header this must stay a
        // plain employee turn.
        var (chatLogic, _, runtime) = Build(
            enabledAgents: ["SupervisorAgent", "KnowledgeAgent"],
            currentUser: new CurrentUserContext { Mobile = "9999888877", IsAdmin = "Y" },
            hasExplicitAdminHeader: false);

        var response = await chatLogic.SendMessageAsync(Request("9999999999", "hello"));

        Assert.DoesNotContain("LeaveApprovalAgent", response.EnabledAgents, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessageAsync_NonAdmin_NeverGetsLeaveApprovalAgent()
    {
        var (chatLogic, _, runtime) = Build(
            enabledAgents: ["SupervisorAgent", "KnowledgeAgent"],
            currentUser: new CurrentUserContext { Mobile = "9999888877", IsAdmin = "N" },
            hasExplicitAdminHeader: true);

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
            currentUser: new CurrentUserContext { Mobile = "9999888877", IsAdmin = "Y" },
            hasExplicitAdminHeader: true);

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
        bool hasExplicitAdminHeader = false)
    {
        var access = new SpyAgentAccessService(enabledAgents);
        var runtime = new SpyChatRuntime();

        var httpContext = new DefaultHttpContext();
        if (hasExplicitAdminHeader)
        {
            httpContext.Request.Headers.Authorization = "Bearer test-token";
        }

        var serviceContext = new FakeServiceContext { CurrentUser = currentUser, RequestContext = httpContext };
        var chatLogic = new ChatLogic(access, runtime, new StubUserProfileRepo(), serviceContext, NullLogger<ChatLogic>.Instance);
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

    private sealed class SpyAgentAccessService(IReadOnlyList<string> result) : IAgentAccessService
    {
        public bool WasCalled { get; private set; }
        public string? LastMobile { get; private set; }

        public Task<IReadOnlyList<string>> GetEnabledAgentNamesAsync(
            string? mobile, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastMobile = mobile;
            return Task.FromResult<IReadOnlyList<string>>(result);
        }
    }

    private sealed class SpyChatRuntime : IHrmsChatRuntime
    {
        public bool WasCalled { get; private set; }
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
    }
}
