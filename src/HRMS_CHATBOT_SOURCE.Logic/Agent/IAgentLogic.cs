using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

namespace HRMS_CHATBOT_SOURCE.Logic;

public interface IAgentLogic
{
    Task<List<UserGroupDto>> GetUserGroupsAsync(CancellationToken cancellationToken = default);

    Task<AgentMasterListResponseDto> GetMasterListAsync(CancellationToken cancellationToken = default);

    Task<AgentMasterDto> UpdateMasterActiveAsync(long agentId, bool isActive, CancellationToken cancellationToken = default);

    Task<AgentControlPanelResponseDto> GetControlPanelAsync(
        string? userGrpCode,
        string? payroll = null,
        CancellationToken cancellationToken = default);

    Task<AgentPayrollMatrixResponseDto> GetPayrollMatrixAsync(
        long agentId,
        CancellationToken cancellationToken = default);

    Task<AgentPayrollAssignmentDto> UpdateGroupActiveAsync(
        long agentId,
        string? userGrpCode,
        string? payroll,
        bool isActive,
        string? createdBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnabledAgentDto>> GetEnabledAgentsByMobileAsync(
        string? mobile,
        CancellationToken cancellationToken = default);
}
