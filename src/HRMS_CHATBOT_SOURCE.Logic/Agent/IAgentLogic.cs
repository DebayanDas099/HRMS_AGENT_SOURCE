using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface IAgentLogic
{
    Task<List<UserGroupDto>> GetUserGroupsAsync(CancellationToken cancellationToken = default);

    Task<AgentMasterListResponseDto> GetMasterListAsync(CancellationToken cancellationToken = default);

    Task<AgentMasterDto> UpdateMasterActiveAsync(long agentId, bool isActive, CancellationToken cancellationToken = default);

    Task<AgentControlPanelResponseDto> GetControlPanelAsync(
        string? userGrpCode,
        CancellationToken cancellationToken = default);

    Task<AgentGroupAssignmentDto> UpdateGroupActiveAsync(
        long agentId,
        string? userGrpCode,
        bool isActive,
        string? createdBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnabledAgentDto>> GetEnabledAgentsAsync(
        string? userGrpCode,
        string? payroll,
        CancellationToken cancellationToken = default);
}
