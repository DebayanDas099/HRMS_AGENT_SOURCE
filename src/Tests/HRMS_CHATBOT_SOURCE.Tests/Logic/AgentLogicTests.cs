using System.Data;
using HRMS_CHATBOT_SOURCE.Domain.Models;
using HRMS_CHATBOT_SOURCE.Logic;
using HRMS_CHATBOT_SOURCE.Repo.Agent;
using Microsoft.Data.SqlClient;

namespace HRMS_CHATBOT_SOURCE.Tests.Logic;

public class AgentLogicTests
{
    [Fact]
    public async Task GetEnabledAgentsByMobileAsync_NoRowsReturned_StaysEmpty()
    {
        // Ground zero for the fail-open bug: an unregistered (or fully-disabled)
        // mobile number gets zero rows back from SQL. The result must stay empty,
        // not be widened to include Supervisor - that widening is exactly what let
        // an unregistered number reach a working conversation.
        var logic = new AgentLogic(new StubAgentRepo(BuildResponse()));

        var agents = await logic.GetEnabledAgentsByMobileAsync("9999999999");

        Assert.Empty(agents);
    }

    [Fact]
    public async Task GetEnabledAgentsByMobileAsync_SomeAgentsEnabled_EnsuresSupervisorIsPresent()
    {
        // A registered number with real access should still be able to reach the
        // router even if the stored procedure's rows do not explicitly list it.
        var logic = new AgentLogic(new StubAgentRepo(BuildResponse(("101", "KnowledgeAgent"))));

        var agents = await logic.GetEnabledAgentsByMobileAsync("9999999999");

        Assert.Contains(agents, agent => agent.AgentName == "SupervisorAgent");
        Assert.Contains(agents, agent => agent.AgentName == "KnowledgeAgent");
    }

    [Fact]
    public async Task GetEnabledAgentsByMobileAsync_SupervisorAlreadyPresent_IsNotDuplicated()
    {
        var logic = new AgentLogic(new StubAgentRepo(BuildResponse(("1", "SupervisorAgent"), ("101", "KnowledgeAgent"))));

        var agents = await logic.GetEnabledAgentsByMobileAsync("9999999999");

        Assert.Single(agents, agent => agent.AgentName == "SupervisorAgent");
    }

    private static MSSQLResponse BuildResponse(params (string Id, string Name)[] rows)
    {
        var table = new DataTable();
        table.Columns.Add("am_id", typeof(long));
        table.Columns.Add("am_name", typeof(string));

        foreach (var (id, name) in rows)
        {
            table.Rows.Add(long.Parse(id), name);
        }

        var dataSet = new DataSet();
        dataSet.Tables.Add(table);

        return new MSSQLResponse
        {
            Data = dataSet,
            OutputParameters =
            [
                new SqlParameter("@outputCode", 1) { Direction = ParameterDirection.Output },
                new SqlParameter("@outputMsg", "OK") { Direction = ParameterDirection.Output }
            ]
        };
    }

    private sealed class StubAgentRepo(MSSQLResponse response) : IAgentRepo
    {
        public Task<MSSQLResponse?> GetUserGroupsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(null);

        public Task<MSSQLResponse?> GetMasterListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(null);

        public Task<MSSQLResponse?> UpdateMasterActiveAsync(long agentId, string active, CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(null);

        public Task<MSSQLResponse?> GetControlPanelAsync(string? userGrpCode, string? payroll = null, CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(null);

        public Task<MSSQLResponse?> GetPayrollMatrixAsync(long agentId, CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(null);

        public Task<MSSQLResponse?> UpdateGroupActiveAsync(
            long agentId, string? userGrpCode, string? payroll, string active, string? createdBy,
            CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(null);

        public Task<MSSQLResponse?> GetEnabledAgentsByMobileAsync(string? mobile, CancellationToken cancellationToken = default)
            => Task.FromResult<MSSQLResponse?>(response);
    }
}
