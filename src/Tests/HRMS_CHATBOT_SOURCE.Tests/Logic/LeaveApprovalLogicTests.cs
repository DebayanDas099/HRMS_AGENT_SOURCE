using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.Logic;
using HRMS_CHATBOT_SOURCE.Repo.Leave;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace HRMS_CHATBOT_SOURCE.Tests.Logic;

public class LeaveApprovalLogicTests
{
    [Fact]
    public async Task GetPendingApplicationsAsync_MapsRowsFromTheStoredProcedure()
    {
        var repo = new FakeLeaveRepo();
        repo.Pending.Add(("ref-1", "9999999999", "Alice", DateTime.Today, DateTime.Today.AddDays(1), "Personal", DateTime.Today.AddDays(-1)));
        var (logic, _) = Build(repo);

        var pending = await logic.GetPendingApplicationsAsync();

        var row = Assert.Single(pending);
        Assert.Equal("ref-1", row.ApplicationReference);
        Assert.Equal("9999999999", row.Mobile);
        Assert.Equal("Alice", row.EmployeeName);
    }

    [Fact]
    public async Task SubmitDecisionsAsync_PersistsEachDecisionAndNotifiesTheEmployee()
    {
        var repo = new FakeLeaveRepo();
        repo.Pending.Add(("ref-1", "9999999999", "Alice", DateTime.Today, DateTime.Today, "Personal", DateTime.Today));
        var (logic, notifier) = Build(repo);

        var processed = await logic.SubmitDecisionsAsync(
            [new LeaveApprovalDecisionDto { ApplicationReference = "ref-1", Decision = "Approved" }],
            approvedByAdminId: "admin-1");

        Assert.Equal(["ref-1"], processed);
        var update = Assert.Single(repo.Updates);
        Assert.Equal("ref-1", update.Reference);
        Assert.Equal("Approved", update.NewStatus);
        Assert.Equal("admin-1", update.ApprovedBy);

        var notification = Assert.Single(notifier.Notifications);
        Assert.Equal("9999999999", notification.Mobile);
        Assert.Equal("ref-1", notification.ApplicationReference);
        Assert.Equal("Approved", notification.NewStatus);
    }

    [Fact]
    public async Task SubmitDecisionsAsync_SkipsAReferenceThatIsNoLongerPending()
    {
        var repo = new FakeLeaveRepo();
        var (logic, notifier) = Build(repo);

        var processed = await logic.SubmitDecisionsAsync(
            [new LeaveApprovalDecisionDto { ApplicationReference = "already-actioned", Decision = "Approved" }],
            approvedByAdminId: "admin-1");

        Assert.Empty(processed);
        Assert.Empty(repo.Updates);
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task SubmitDecisionsAsync_LeavesUntouchedRowsUnprocessed()
    {
        var repo = new FakeLeaveRepo();
        repo.Pending.Add(("ref-1", "9999999999", "Alice", DateTime.Today, DateTime.Today, "Personal", DateTime.Today));
        repo.Pending.Add(("ref-2", "8888888888", "Bob", DateTime.Today, DateTime.Today, "Personal", DateTime.Today));
        var (logic, _) = Build(repo);

        // Only ref-1 is decided; ref-2 is left alone, matching "not clicked anything" in the grid.
        var processed = await logic.SubmitDecisionsAsync(
            [new LeaveApprovalDecisionDto { ApplicationReference = "ref-1", Decision = "Rejected", Remarks = "Not enough coverage that week" }],
            approvedByAdminId: "admin-1");

        Assert.Equal(["ref-1"], processed);
        Assert.Single(repo.Updates);
    }

    [Fact]
    public async Task SubmitDecisionsAsync_SkipsARejectionWithNoRemarks()
    {
        var repo = new FakeLeaveRepo();
        repo.Pending.Add(("ref-1", "9999999999", "Alice", DateTime.Today, DateTime.Today, "Personal", DateTime.Today));
        var (logic, notifier) = Build(repo);

        var processed = await logic.SubmitDecisionsAsync(
            [new LeaveApprovalDecisionDto { ApplicationReference = "ref-1", Decision = "Rejected", Remarks = "   " }],
            approvedByAdminId: "admin-1");

        Assert.Empty(processed);
        Assert.Empty(repo.Updates);
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task SubmitDecisionsAsync_PassesRemarksThroughOnApproval()
    {
        var repo = new FakeLeaveRepo();
        repo.Pending.Add(("ref-1", "9999999999", "Alice", DateTime.Today, DateTime.Today, "Personal", DateTime.Today));
        var (logic, notifier) = Build(repo);

        await logic.SubmitDecisionsAsync(
            [new LeaveApprovalDecisionDto { ApplicationReference = "ref-1", Decision = "Approved", Remarks = "Enjoy!" }],
            approvedByAdminId: "admin-1");

        Assert.Equal("Enjoy!", Assert.Single(repo.Updates).Note);
        Assert.Equal("Enjoy!", Assert.Single(notifier.Notifications).Note);
    }

    private static (LeaveApprovalLogic Logic, FakeNotifier Notifier) Build(FakeLeaveRepo repo)
    {
        var notifier = new FakeNotifier();
        var logic = new LeaveApprovalLogic(repo, notifier, NullLogger<LeaveApprovalLogic>.Instance);
        return (logic, notifier);
    }

    private sealed class FakeLeaveRepo : ILeaveRepo
    {
        public List<(string Reference, string Mobile, string Name, DateTime From, DateTime To, string Reason, DateTime AppliedOn)> Pending { get; } = [];
        public List<(string Reference, string NewStatus, string ApprovedBy, string? Note)> Updates { get; } = [];

        public Task<MSSQLResponse?> GetLeaveDetailsByUserAsync(string? mobile, DateTime startDate, DateTime endDate, string? leaveCategory, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MSSQLResponse?> ValidateAndApplyLeaveByUserAsync(string? mobile, DateTime startDate, DateTime endDate, string? leaveType, string? reason, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MSSQLResponse?> GetHolidayListAsync(DateTime startDate, DateTime endDate, int? maxResults, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MSSQLResponse?> GetPendingLeaveApplicationsAsync(CancellationToken cancellationToken = default)
        {
            var table = new DataTable();
            table.Columns.Add("application_reference", typeof(string));
            table.Columns.Add("mobile", typeof(string));
            table.Columns.Add("employee_name", typeof(string));
            table.Columns.Add("from_date", typeof(DateTime));
            table.Columns.Add("to_date", typeof(DateTime));
            table.Columns.Add("reason", typeof(string));
            table.Columns.Add("applied_on", typeof(DateTime));

            foreach (var row in Pending)
            {
                table.Rows.Add(row.Reference, row.Mobile, row.Name, row.From, row.To, row.Reason, row.AppliedOn);
            }

            var dataSet = new DataSet();
            dataSet.Tables.Add(table);

            return Task.FromResult<MSSQLResponse?>(new MSSQLResponse
            {
                Data = dataSet,
                OutputParameters = SuccessOutputParams()
            });
        }

        public Task<MSSQLResponse?> UpdateLeaveApplicationStatusAsync(
            string applicationReference,
            string newStatus,
            string approvedBy,
            string? note,
            CancellationToken cancellationToken = default)
        {
            Updates.Add((applicationReference, newStatus, approvedBy, note));
            return Task.FromResult<MSSQLResponse?>(new MSSQLResponse
            {
                RowsAffected = 1,
                OutputParameters = SuccessOutputParams()
            });
        }

        private static SqlParameter[] SuccessOutputParams() =>
        [
            new() { ParameterName = "@outputCode", Value = 1 },
            new() { ParameterName = "@outputMsg", Value = "OK" }
        ];
    }

    private sealed class FakeNotifier : ILeaveStatusChangeNotifier
    {
        public List<(string Mobile, string ApplicationReference, string NewStatus, string? Note)> Notifications { get; } = [];

        public Task NotifyAsync(string mobile, string applicationReference, string newStatus, string? note, CancellationToken cancellationToken = default)
        {
            Notifications.Add((mobile, applicationReference, newStatus, note));
            return Task.CompletedTask;
        }
    }
}
