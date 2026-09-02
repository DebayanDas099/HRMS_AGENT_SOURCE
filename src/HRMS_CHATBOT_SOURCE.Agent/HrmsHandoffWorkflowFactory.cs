using HRMS_CHATBOT_SOURCE.Agent.Skills;
using HRMS_CHATBOT_SOURCE.Agent.Tools;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Logic.Common;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS_CHATBOT_SOURCE.Agent;

public sealed class HrmsHandoffWorkflowFactory
{
    private const string SpecialistStubNote =
        "You do not call real HR backend systems in this test slice. "
        + "Stay in your role, complete the turn, or hand off back to SupervisorAgent when the request is out of scope.";

    private readonly IChatClient _chatClient;
    private readonly HandoffWorkflowTemplate _blueprintTemplate;
    private readonly PolicyKnowledgeTools _policyKnowledgeTools;
    private readonly LeaveApplicationTools _leaveApplicationTools;
    private readonly DocumentAgentTools _documentAgentTools;
    private readonly RelativeDateParsingTools _relativeDateParsingTools;
    private readonly IServiceScopeFactory _scopeFactory;

    public HrmsHandoffWorkflowFactory(
        IChatClient chatClient,
        HandoffWorkflowTemplate blueprintTemplate,
        PolicyKnowledgeTools policyKnowledgeTools,
        LeaveApplicationTools leaveApplicationTools,
        DocumentAgentTools documentAgentTools,
        RelativeDateParsingTools relativeDateParsingTools,
        IServiceScopeFactory scopeFactory)
    {
        _chatClient = chatClient;
        _blueprintTemplate = blueprintTemplate;
        _policyKnowledgeTools = policyKnowledgeTools;
        _leaveApplicationTools = leaveApplicationTools;
        _documentAgentTools = documentAgentTools;
        _relativeDateParsingTools = relativeDateParsingTools;
        _scopeFactory = scopeFactory;
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
            return 
            [
                AIFunctionFactory.Create(_policyKnowledgeTools.SearchPolicyDocumentsAsync)
            ];
        }

        if (string.Equals(agentName, AgentNames.LeaveApplication, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                AIFunctionFactory.Create(_relativeDateParsingTools.ParseRelativeDateRange),
                AIFunctionFactory.Create(_leaveApplicationTools.GetLeaveStatusAsync),
                AIFunctionFactory.Create(_leaveApplicationTools.ValidateAndApplyLeaveAsync)
            ];
        }

        if (string.Equals(agentName, AgentNames.Document, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                AIFunctionFactory.Create(_documentAgentTools.SearchDocumentsBySimilarityAsync),
                AIFunctionFactory.Create(_documentAgentTools.BuildDocumentDownloadLink)
            ];
        }

        if (string.Equals(agentName, AgentNames.Supervisor, StringComparison.OrdinalIgnoreCase))
        {
            return 
            [
                AIFunctionFactory.Create(_relativeDateParsingTools.ParseRelativeDateRange)
            ];
        }

        return null;
    }

    private static bool HasTools(string agentName)
    {
        return string.Equals(agentName, AgentNames.Knowledge, StringComparison.OrdinalIgnoreCase)
            || string.Equals(agentName, AgentNames.LeaveApplication, StringComparison.OrdinalIgnoreCase)
            || string.Equals(agentName, AgentNames.Document, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveInstructions(AgentBlueprint participant)
    {
        var skill = !string.IsNullOrWhiteSpace(participant.Instructions)
                ? participant.Instructions
                : File.Exists(participant.SkillPath)
                    ? File.ReadAllText(participant.SkillPath)
                    : $"You are {participant.Name}.";

        using var scope = _scopeFactory.CreateScope();
        var commonLogic = scope.ServiceProvider.GetRequiredService<ICommonLogic>();
        skill = AgentInstructionComposer.PrependRuntimeContext(skill, commonLogic.GetReferenceDateTime());

        if (string.Equals(participant.Name, AgentNames.Supervisor, StringComparison.OrdinalIgnoreCase)
            || HasTools(participant.Name))
        {
            return skill;
        }

        return skill + Environment.NewLine + Environment.NewLine + SpecialistStubNote;
    }
}
