namespace HRMS_CHATBOT_SOURCE.Logic;

public class AgentAccessService : IAgentAccessService
{
    private readonly IAgentLogic _agentLogic;

    public AgentAccessService(IAgentLogic agentLogic)
    {
        _agentLogic = agentLogic;
    }

    public async Task<IReadOnlyList<string>> GetEnabledAgentNamesAsync(
        string? mobile,
        CancellationToken cancellationToken = default)
    {
        var agents = await _agentLogic.GetEnabledAgentsByMobileAsync(mobile, cancellationToken);
        return agents
            .Select(agent => agent.AgentName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
