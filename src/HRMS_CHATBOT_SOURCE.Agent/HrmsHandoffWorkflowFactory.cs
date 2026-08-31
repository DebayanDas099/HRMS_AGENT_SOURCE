using HRMS_CHATBOT_SOURCE.Agent.Skills;
using HRMS_CHATBOT_SOURCE.Agent.Tools;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace HRMS_CHATBOT_SOURCE.Agent;

public sealed class HrmsHandoffWorkflowFactory
{
    private const string SpecialistStubNote =
        "You do not call real HR backend systems in this test slice. "
        + "Stay in your role, complete the turn, or hand off back to SupervisorAgent when the request is out of scope.";

    private readonly IChatClient _chatClient;
    private readonly HandoffWorkflowTemplate _blueprintTemplate;
    private readonly PolicyKnowledgeTools _policyKnowledgeTools;

    public HrmsHandoffWorkflowFactory(
        IChatClient chatClient,
        HandoffWorkflowTemplate blueprintTemplate,
        PolicyKnowledgeTools policyKnowledgeTools)
    {
        _chatClient = chatClient;
        _blueprintTemplate = blueprintTemplate;
        _policyKnowledgeTools = policyKnowledgeTools;
    }

    public Workflow Build(IReadOnlyCollection<string> enabledAgentNames)
    {
        var blueprint = _blueprintTemplate.DescribeBlueprint(enabledAgentNames);
        return Build(blueprint);
    }

    public Workflow Build(AgentFrameworkBlueprint blueprint)
    {
        var agents = blueprint.Participants.ToDictionary(
            participant => participant.Name,
            participant => CreateAgent(participant),
            StringComparer.OrdinalIgnoreCase);

        if (!agents.TryGetValue(AgentNames.Supervisor, out var supervisor))
        {
            throw new InvalidOperationException("SupervisorAgent is required in the handoff blueprint.");
        }

        var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(supervisor);
        foreach (var source in blueprint.Handoffs)
        {
            if (!agents.TryGetValue(source.Key, out var sourceAgent))
            {
                continue;
            }

            var targets = source.Value
                .Where(agents.ContainsKey)
                .Select(name => agents[name])
                .ToList();
            if (targets.Count == 0)
            {
                continue;
            }

            builder.WithHandoffs(sourceAgent, targets);
        }

        return builder.Build();
    }

    internal static IReadOnlyList<string> GetSupervisorHandoffTargets(AgentFrameworkBlueprint blueprint)
    {
        return blueprint.Handoffs.TryGetValue(AgentNames.Supervisor, out var targets)
            ? targets
            : [];
    }

    private ChatClientAgent CreateAgent(AgentBlueprint participant)
    {
        var instructions = ResolveInstructions(participant);
        var tools = ResolveTools(participant.Name);

        return (ChatClientAgent)_chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Id = participant.Name,
            Name = participant.Name,
            Description = AgentSkillCatalog.Describe(participant.Name),
            ChatOptions = new ChatOptions
            {
                Instructions = instructions,
                Tools = tools
            }
        });
    }

    /// <summary>
    /// Tools are granted per agent, never globally. An agent that cannot reach a
    /// capability cannot be talked into using it.
    /// </summary>
    private IList<AITool>? ResolveTools(string agentName)
    {
        if (string.Equals(agentName, AgentNames.Knowledge, StringComparison.OrdinalIgnoreCase))
        {
            return [AIFunctionFactory.Create(_policyKnowledgeTools.SearchPolicyDocumentsAsync)];
        }

        return null;
    }

    private static bool HasTools(string agentName)
    {
        return string.Equals(agentName, AgentNames.Knowledge, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveInstructions(AgentBlueprint participant)
    {
        if (!string.IsNullOrWhiteSpace(participant.Instructions))
        {
            return participant.Instructions;
        }

        var skill = File.Exists(participant.SkillPath)
            ? File.ReadAllText(participant.SkillPath)
            : $"You are {participant.Name}.";

        if (string.Equals(participant.Name, AgentNames.Supervisor, StringComparison.OrdinalIgnoreCase)
            || HasTools(participant.Name))
        {
            return skill;
        }

        return skill + Environment.NewLine + Environment.NewLine + SpecialistStubNote;
    }
}
