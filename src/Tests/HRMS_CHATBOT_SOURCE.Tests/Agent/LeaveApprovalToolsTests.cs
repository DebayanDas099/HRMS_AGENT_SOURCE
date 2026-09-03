using HRMS_CHATBOT_SOURCE.Agent.Tools;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Infrastructure.Core;
using HRMS_CHATBOT_SOURCE.Logic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class LeaveApprovalToolsTests
{
    [Fact]
    public async Task GetPendingLeaveApplicationsAsync_ListsEachApplication()
    {
        var (tools, logic, _) = Build(isAdmin: true);
        logic.Pending.Add(new PendingLeaveApplicationDto
        {
            ApplicationReference = "ref-1",
            EmployeeName = "Alice",
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 2),
            Reason = "Personal",
            AppliedOn = new DateTime(2025, 12, 30)
        });

        var result = await tools.GetPendingLeaveApplicationsAsync();

        Assert.Contains("ref-1", result);
        Assert.Contains("Alice", result);
    }

    [Fact]
    public async Task GetPendingLeaveApplicationsAsync_NoneAwaiting_SaysSo()
    {
        var (tools, _, _) = Build(isAdmin: true);

        var result = await tools.GetPendingLeaveApplicationsAsync();

        Assert.Contains("No leave applications", result);
    }

    [Fact]
    public async Task SubmitLeaveDecisionAsync_WithoutAVerifiedAdminSession_RefusesAndDoesNotCallLogic()
    {
        var (tools, logic, _) = Build(isAdmin: false);

        var result = await tools.SubmitLeaveDecisionAsync("ref-1", "Approved", null);

        Assert.Contains("not authorized", result, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(logic.Submitted);
    }

    [Fact]
    public async Task SubmitLeaveDecisionAsync_Approve_PassesThroughAndResolvesApproverServerSide()
    {
        var (tools, logic, _) = Build(isAdmin: true, userId: "admin-42");
        logic.ProcessResult = ["ref-1"];

        var result = await tools.SubmitLeaveDecisionAsync("ref-1", "Approved", "Enjoy!");

        Assert.Contains("Approved successfully", result);
        var call = Assert.Single(logic.Submitted);
        Assert.Equal("admin-42", call.ApprovedBy);
        Assert.Equal("ref-1", call.Decisions[0].ApplicationReference);
        Assert.Equal("Enjoy!", call.Decisions[0].Remarks);
    }

    [Fact]
    public async Task SubmitLeaveDecisionAsync_RejectWithoutRemarks_ReportsRemarksRequired()
    {
        var (tools, logic, _) = Build(isAdmin: true);
        logic.ProcessResult = []; // logic layer itself skips reject-without-remarks

        var result = await tools.SubmitLeaveDecisionAsync("ref-1", "Rejected", null);

        Assert.Contains("Remarks are required", result);
    }

    [Fact]
    public async Task SubmitLeaveDecisionAsync_UnknownReference_ReportsCouldNotProcess()
    {
        var (tools, logic, _) = Build(isAdmin: true);
        logic.ProcessResult = [];

        var result = await tools.SubmitLeaveDecisionAsync("ref-1", "Approved", null);

        Assert.Contains("Could not process", result);
    }

    private static (LeaveApprovalTools Tools, FakeLeaveApprovalLogic Logic, IServiceContext ServiceContext) Build(
        bool isAdmin,
        string? userId = "admin-1")
    {
        var logic = new FakeLeaveApprovalLogic();
        var serviceContext = new FakeServiceContext
        {
            CurrentUser = new CurrentUserContext { IsAdmin = isAdmin ? "Y" : "N", UserId = userId }
        };

        var services = new ServiceCollection()
            .AddScoped<ILeaveApprovalLogic>(_ => logic)
            .AddScoped<IServiceContext>(_ => serviceContext)
            .BuildServiceProvider();

        var tools = new LeaveApprovalTools(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<LeaveApprovalTools>.Instance);

        return (tools, logic, serviceContext);
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

    private sealed class FakeLeaveApprovalLogic : ILeaveApprovalLogic
    {
        public List<PendingLeaveApplicationDto> Pending { get; } = [];
        public List<(IReadOnlyList<LeaveApprovalDecisionDto> Decisions, string ApprovedBy)> Submitted { get; } = [];
        public List<string> ProcessResult { get; set; } = [];

        public Task<List<PendingLeaveApplicationDto>> GetPendingApplicationsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Pending);

        public Task<List<string>> SubmitDecisionsAsync(
            IReadOnlyList<LeaveApprovalDecisionDto> decisions,
            string approvedByAdminId,
            CancellationToken cancellationToken = default)
        {
            Submitted.Add((decisions, approvedByAdminId));
            return Task.FromResult(ProcessResult);
        }
    }
}
