using System.ComponentModel.DataAnnotations;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic.Adapter;
using HRMS_CHATBOT_SOURCE.Repo.Agent;

namespace HRMS_CHATBOT_SOURCE.Logic;

public class AgentLogic : IAgentLogic
{
    private readonly IAgentRepo _agentRepo;

    public AgentLogic(IAgentRepo agentRepo)
    {
        _agentRepo = agentRepo;
    }

    public async Task<List<UserGroupDto>> GetUserGroupsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _agentRepo.GetUserGroupsAsync(cancellationToken);
        return AgentAdapter.MapUserGroups(response);
    }

    public async Task<AgentMasterListResponseDto> GetMasterListAsync(CancellationToken cancellationToken = default)
    {
        var response = await _agentRepo.GetMasterListAsync(cancellationToken);
        var items = AgentAdapter.MapMasterList(response);
        var activeCount = items.Count(item => string.Equals(item.Active, "Y", StringComparison.OrdinalIgnoreCase));

        return new AgentMasterListResponseDto
        {
            TotalAgents = items.Count,
            ActiveAgents = activeCount,
            InactiveAgents = items.Count - activeCount,
            Items = items
        };
    }

    public async Task<AgentMasterDto> UpdateMasterActiveAsync(
        long agentId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        if (agentId <= 0)
        {
            throw new ValidationException("Invalid agent id.");
        }

        var response = await _agentRepo.UpdateMasterActiveAsync(agentId, isActive ? "Y" : "N", cancellationToken);
        AgentAdapter.EnsureSuccess(response);

        var list = await GetMasterListAsync(cancellationToken);
        var updated = list.Items.FirstOrDefault(item => item.AgentId == agentId);
        if (updated == null)
        {
            throw new ValidationException("Agent model was updated but could not be reloaded.");
        }

        return updated;
    }

    public async Task<AgentControlPanelResponseDto> GetControlPanelAsync(
        string? userGrpCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userGrpCode))
        {
            throw new ValidationException("User group code is required.");
        }

        var assignmentsResponse = await _agentRepo.GetControlPanelAsync(userGrpCode.Trim(), cancellationToken);
        var assignments = AgentAdapter.MapAssignments(assignmentsResponse);

        var activeCount = assignments.Count(item =>
            string.Equals(item.GroupActive, "Y", StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.MasterActive, "Y", StringComparison.OrdinalIgnoreCase));

        return new AgentControlPanelResponseDto
        {
            UserGrpCode = userGrpCode.Trim(),
            TotalAgents = assignments.Count,
            ActiveAgents = activeCount,
            InactiveAgents = assignments.Count - activeCount,
            Items = assignments
        };
    }

    public async Task<AgentGroupAssignmentDto> UpdateGroupActiveAsync(
        long agentId,
        string? userGrpCode,
        bool isActive,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        if (agentId <= 0)
        {
            throw new ValidationException("Invalid agent id.");
        }

        if (string.IsNullOrWhiteSpace(userGrpCode))
        {
            throw new ValidationException("User group code is required.");
        }

        var groupCode = userGrpCode.Trim();
        var response = await _agentRepo.UpdateGroupActiveAsync(
            agentId,
            groupCode,
            isActive ? "Y" : "N",
            createdBy,
            cancellationToken);
        AgentAdapter.EnsureSuccess(response);

        var panel = await GetControlPanelAsync(groupCode, cancellationToken);
        var updated = panel.Items.FirstOrDefault(item => item.AgentId == agentId);
        if (updated == null)
        {
            throw new ValidationException("Agent assignment was updated but could not be reloaded.");
        }

        return updated;
    }

    public async Task<IReadOnlyList<EnabledAgentDto>> GetEnabledAgentsAsync(
        string? userGrpCode,
        string? payroll,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userGrpCode))
        {
            throw new ValidationException("User group code is required.");
        }

        var response = await _agentRepo.GetEnabledAgentsAsync(userGrpCode.Trim(), payroll, cancellationToken);
        var agents = AgentAdapter.MapEnabledAgents(response);

        if (!agents.Any(agent => string.Equals(agent.AgentName, AgentNames.Supervisor, StringComparison.OrdinalIgnoreCase)))
        {
            return agents
                .Prepend(new EnabledAgentDto { AgentName = AgentNames.Supervisor })
                .ToList();
        }

        return agents;
    }
}
