using HRMS_CHATBOT_SOURCE.Agent.Skills;
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

    public HrmsHandoffWorkflowFactory(IChatClient chatClient, HandoffWorkflowTemplate blueprintTemplate)
    {
        _chatClient = chatClient;
        _blueprintTemplate = blueprintTemplate;
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
        return (ChatClientAgent)_chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Id = participant.Name,
            Name = participant.Name,
            Description = AgentSkillCatalog.Describe(participant.Name),
            ChatOptions = new ChatOptions
            {
                Instructions = instructions
            }
        });
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

        if (string.Equals(participant.Name, AgentNames.Supervisor, StringComparison.OrdinalIgnoreCase))
        {
            return skill;
        }

        return skill + Environment.NewLine + Environment.NewLine + SpecialistStubNote;
    }
}
