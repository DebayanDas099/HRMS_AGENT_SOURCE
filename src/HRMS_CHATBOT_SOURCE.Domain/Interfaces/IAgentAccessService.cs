namespace HRMS_CHATBOT_SOURCE.Domain.Interfaces;

public interface IAgentAccessService
{
    Task<IReadOnlyList<string>> GetEnabledAgentNamesAsync(
        string? groupCode,
        string? payroll = null,
        CancellationToken cancellationToken = default);
}
