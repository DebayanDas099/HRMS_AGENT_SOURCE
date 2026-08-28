using HRMS_CHATBOT_SOURCE.Domain.Models;

namespace HRMS_CHATBOT_SOURCE.Repo.Agent;

public interface IAgentRepo
{
    Task<MSSQLResponse?> GetUserGroupsAsync(CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetMasterListAsync(CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> UpdateMasterActiveAsync(long agentId, string active, CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetControlPanelAsync(string? userGrpCode, CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> UpdateGroupActiveAsync(
        long agentId,
        string? userGrpCode,
        string active,
        string? createdBy,
        CancellationToken cancellationToken = default);

    Task<MSSQLResponse?> GetEnabledAgentsAsync(
        string? userGrpCode,
        string? payroll,
        CancellationToken cancellationToken = default);
}
