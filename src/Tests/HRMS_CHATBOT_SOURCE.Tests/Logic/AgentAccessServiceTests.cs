using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic;

namespace HRMS_CHATBOT_SOURCE.Tests.Logic;

public class AgentAccessServiceTests
{
    [Fact]
    public async Task GetWorkflowAgentNamesAsync_ExcludesVoiceInputAgent()
    {
        var logic = new StubAgentLogic([
            new EnabledAgentDto { AgentName = AgentNames.Supervisor },
            new EnabledAgentDto { AgentName = AgentNames.Knowledge },
            new EnabledAgentDto { AgentName = AgentNames.VoiceInput }
        ]);
        var service = new AgentAccessService(logic);

        var agents = await service.GetWorkflowAgentNamesAsync("9999999999");

        Assert.Contains(AgentNames.Supervisor, agents, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(AgentNames.Knowledge, agents, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(AgentNames.VoiceInput, agents, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IsVoiceInputEnabledAsync_ReturnsTrueWhenVoiceAgentPresent()
    {
        var logic = new StubAgentLogic([
            new EnabledAgentDto { AgentName = AgentNames.VoiceInput }
        ]);
        var service = new AgentAccessService(logic);

        var enabled = await service.IsVoiceInputEnabledAsync("9999999999");

        Assert.True(enabled);
    }

    [Fact]
    public async Task IsVoiceInputEnabledAsync_ReturnsFalseWhenVoiceAgentMissing()
    {
        var logic = new StubAgentLogic([
            new EnabledAgentDto { AgentName = AgentNames.Knowledge }
        ]);
        var service = new AgentAccessService(logic);

        var enabled = await service.IsVoiceInputEnabledAsync("9999999999");

        Assert.False(enabled);
    }

    private sealed class StubAgentLogic(IReadOnlyList<EnabledAgentDto> agents) : IAgentLogic
    {
        public Task<List<UserGroupDto>> GetUserGroupsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<UserGroupDto>());

        public Task<AgentMasterListResponseDto> GetMasterListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentMasterListResponseDto());

        public Task<AgentMasterDto> UpdateMasterActiveAsync(long agentId, bool isActive, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentMasterDto());

        public Task<AgentControlPanelResponseDto> GetControlPanelAsync(string? userGrpCode, string? payroll = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentControlPanelResponseDto());

        public Task<AgentPayrollMatrixResponseDto> GetPayrollMatrixAsync(long agentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentPayrollMatrixResponseDto());

        public Task<AgentPayrollAssignmentDto> UpdateGroupActiveAsync(
            long agentId,
            string? userGrpCode,
            string? payroll,
            bool isActive,
            string? createdBy,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentPayrollAssignmentDto());

        public Task<IReadOnlyList<EnabledAgentDto>> GetEnabledAgentsByMobileAsync(
            string? mobile,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(agents);
    }
}
