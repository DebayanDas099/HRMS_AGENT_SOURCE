using HRMS_CHATBOT_SOURCE.Agent.Configuration;
using HRMS_CHATBOT_SOURCE.Domain.Constants;

namespace HRMS_CHATBOT_SOURCE.Agent.Skills;

/// <summary>
/// Capability summaries for specialist agents that can be attached to the Supervisor as skills.
/// </summary>
public static class AgentSkillCatalog
{
    public static readonly IReadOnlyDictionary<string, string> CapabilitySummaries =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [AgentNames.LeaveApplication] =
                "leave balance, leave application, leave status, leave approval, and company holiday list",
            [AgentNames.Document] =
                "document repository, uploads, ingestion status, and repository metadata",
            [AgentNames.Knowledge] =
                "HR policy, training, and knowledge-base questions"
        };

    public static IReadOnlyList<string> SpecialistAgentNames =>
        AgentHandoffTopology.OutboundHandoffs.TryGetValue(AgentNames.Supervisor, out var specialists)
            ? specialists
            : [];

    public static string Describe(string agentName)
    {
        return CapabilitySummaries.TryGetValue(agentName, out var summary)
            ? summary
            : "specialist HR workflow";
    }
}
