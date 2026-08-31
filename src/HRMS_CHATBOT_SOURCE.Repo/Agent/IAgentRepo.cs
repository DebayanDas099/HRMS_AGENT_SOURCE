using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Repo.Agent;

public interface IAgentRepo
{
    Task<MSSQLResponse?> GetUserGroupsAsync(CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetMasterListAsync(CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> UpdateMasterActiveAsync(long agentId, string active, CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetControlPanelAsync(
        string? userGrpCode,
        string? payroll = null,
        CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetPayrollMatrixAsync(long agentId, CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> UpdateGroupActiveAsync(
        long agentId,
        string? userGrpCode,
        string? payroll,
        string active,
        string? createdBy,
        CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetEnabledAgentsByMobileAsync(
        string? mobile,
        CancellationToken cancellationToken = default);
}
