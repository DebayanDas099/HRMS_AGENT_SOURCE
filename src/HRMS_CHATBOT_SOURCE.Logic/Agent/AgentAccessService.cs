using HRMS_CHATBOT_SOURCE.Domain.Constants;

namespace HRMS_CHATBOT_SOURCE.Logic;

public class AgentAccessService : IAgentAccessService
{
    private readonly IAgentLogic _agentLogic;

    public AgentAccessService(IAgentLogic agentLogic)
    {
        _agentLogic = agentLogic;
    }

    public Task<IReadOnlyList<string>> GetEnabledAgentNamesAsync(
        string? mobile,
        CancellationToken cancellationToken = default) =>
        GetWorkflowAgentNamesAsync(mobile, cancellationToken);

    public async Task<IReadOnlyList<string>> GetWorkflowAgentNamesAsync(
        string? mobile,
        CancellationToken cancellationToken = default)
    {
        var agents = await _agentLogic.GetEnabledAgentsByMobileAsync(mobile, cancellationToken);
        return agents
            .Select(agent => agent.AgentName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => !string.Equals(name, AgentNames.VoiceInput, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<bool> IsVoiceInputEnabledAsync(
        string mobile,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return false;
        }

        var agents = await _agentLogic.GetEnabledAgentsByMobileAsync(mobile.Trim(), cancellationToken);
        return agents.Any(agent =>
            string.Equals(agent.AgentName, AgentNames.VoiceInput, StringComparison.OrdinalIgnoreCase));
    }
}
