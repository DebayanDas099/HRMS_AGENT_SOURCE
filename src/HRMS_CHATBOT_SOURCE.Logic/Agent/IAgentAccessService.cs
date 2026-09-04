namespace HRMS_CHATBOT_SOURCE.Logic;

public interface IAgentAccessService
{
    Task<IReadOnlyList<string>> GetEnabledAgentNamesAsync(
        string? mobile,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetWorkflowAgentNamesAsync(
        string? mobile,
        CancellationToken cancellationToken = default);

    Task<bool> IsVoiceInputEnabledAsync(
        string mobile,
        CancellationToken cancellationToken = default);
}
