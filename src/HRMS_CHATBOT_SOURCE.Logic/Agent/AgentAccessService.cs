using HRMS_CHATBOT_SOURCE.Domain.Interfaces;
using HRMS_CHATBOT_SOURCE.Logic;

namespace HRMS_CHATBOT_SOURCE.Logic.Agent;

public class AgentAccessService : IAgentAccessService
{
    private readonly IAgentLogic _agentLogic;

    public AgentAccessService(IAgentLogic agentLogic)
    {
        _agentLogic = agentLogic;
    }

    public async Task<IReadOnlyList<string>> GetEnabledAgentNamesAsync(
        string? groupCode,
        string? payroll = null,
        CancellationToken cancellationToken = default)
    {
        var agents = await _agentLogic.GetEnabledAgentsAsync(groupCode, payroll, cancellationToken);
        return agents
            .Select(agent => agent.AgentName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
