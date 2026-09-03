namespace HRMS_CHATBOT_SOURCE.Agent.Configuration;

/// <summary>
/// Declarative handoff topology for the HRMS multi-agent workflow.
/// Agents are not instantiated here; this type documents routing only.
/// </summary>
public static class AgentHandoffTopology
{
    public const string WorkflowName = "HrmsHandoffWorkflow";

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> OutboundHandoffs =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [Domain.Constants.AgentNames.Supervisor] =
            [
                Domain.Constants.AgentNames.LeaveApplication,
                Domain.Constants.AgentNames.Document,
                Domain.Constants.AgentNames.Knowledge,
                Domain.Constants.AgentNames.LeaveApproval
            ],
            [Domain.Constants.AgentNames.LeaveApplication] =
            [
                Domain.Constants.AgentNames.Supervisor
            ],
            [Domain.Constants.AgentNames.LeaveApproval] =
            [
                Domain.Constants.AgentNames.Supervisor
            ],
            [Domain.Constants.AgentNames.Document] =
            [
                Domain.Constants.AgentNames.Supervisor
            ],
            [Domain.Constants.AgentNames.Knowledge] =
            [
                Domain.Constants.AgentNames.Supervisor
            ]
        };

    public static readonly string StartAgent = Domain.Constants.AgentNames.Supervisor;
}
