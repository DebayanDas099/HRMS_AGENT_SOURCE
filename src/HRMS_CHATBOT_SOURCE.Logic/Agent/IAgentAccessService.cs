namespace HRMS_CHATBOT_SOURCE.Logic;

public interface IAgentAccessService
{
    Task<IReadOnlyList<string>> GetEnabledAgentNamesAsync(
        string? mobile,
        CancellationToken cancellationToken = default);
}
