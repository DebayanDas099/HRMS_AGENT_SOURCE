using HRMS_CHATBOT_SOURCE.Agent.Configuration;
using HRMS_CHATBOT_SOURCE.Agent.Skills;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HRMS_CHATBOT_SOURCE.Agent;

/// <summary>
/// Template for a Microsoft Agent Framework handoff workflow backed by Azure AI Foundry.
/// Agent instances are intentionally not created yet.
/// </summary>
public sealed class HandoffWorkflowTemplate
{
    private readonly AgentFoundrySettings _foundrySettings;
    private readonly ILogger<HandoffWorkflowTemplate> _logger;

    public HandoffWorkflowTemplate(
        IOptions<AgentFoundrySettings> foundryOptions,
        ILogger<HandoffWorkflowTemplate> logger)
    {
        _foundrySettings = foundryOptions.Value;
        _logger = logger;
    }

    public AgentFrameworkBlueprint DescribeBlueprint()
    {
        return DescribeBlueprint(enabledAgentNames: null);
    }

    public AgentFrameworkBlueprint DescribeBlueprint(IReadOnlyCollection<string>? enabledAgentNames)
    {
        var blueprint = new AgentFrameworkBlueprint
        {
            WorkflowName = AgentHandoffTopology.WorkflowName,
            StartAgent = AgentHandoffTopology.StartAgent,
            ChatModel = _foundrySettings.ChatDeployment ?? _foundrySettings.ChatModel,
            EmbeddingModel = _foundrySettings.EmbeddingModel,
            Participants =
            [
                new AgentBlueprint
                {
                    Name = AgentNames.Supervisor,
                    SkillPath = AgentSkillPaths.GetSkillPath(AgentNames.Supervisor),
                    Role = "Coordinator"
                },
                new AgentBlueprint
                {
                    Name = AgentNames.LeaveApplication,
                    SkillPath = AgentSkillPaths.GetSkillPath(AgentNames.LeaveApplication),
                    Role = "Specialist"
                },
                new AgentBlueprint
                {
                    Name = AgentNames.Document,
                    SkillPath = AgentSkillPaths.GetSkillPath(AgentNames.Document),
                    Role = "Specialist"
                },
                new AgentBlueprint
                {
                    Name = AgentNames.Knowledge,
                    SkillPath = AgentSkillPaths.GetSkillPath(AgentNames.Knowledge),
                    Role = "Specialist"
                }
            ],
            Handoffs = AgentHandoffTopology.OutboundHandoffs
        };

        return FilterBlueprint(blueprint, enabledAgentNames);
    }

    internal static AgentFrameworkBlueprint FilterBlueprint(
        AgentFrameworkBlueprint blueprint,
        IReadOnlyCollection<string>? enabledAgentNames)
    {
        var catalogNames = enabledAgentNames
            ?? blueprint.Participants.Select(participant => participant.Name).ToList();

        IReadOnlyList<AgentBlueprint> participants = blueprint.Participants;
        IReadOnlyDictionary<string, IReadOnlyList<string>> handoffs = blueprint.Handoffs;

        if (enabledAgentNames != null)
        {
            var enabled = new HashSet<string>(enabledAgentNames, StringComparer.OrdinalIgnoreCase)
            {
                AgentNames.Supervisor
            };

            participants = blueprint.Participants
                .Where(participant => enabled.Contains(participant.Name))
                .ToList();

            var participantNames = participants
                .Select(participant => participant.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            handoffs = blueprint.Handoffs
                .Where(pair => participantNames.Contains(pair.Key))
                .ToDictionary(
                    pair => pair.Key,
                    pair => (IReadOnlyList<string>)pair.Value
                        .Where(target => participantNames.Contains(target))
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        var supervisorSkill = LoadSupervisorSkill(participants);
        var supervisorInstructions = SupervisorSkillComposer.Compose(supervisorSkill, catalogNames);

        participants = participants
            .Select(participant => string.Equals(participant.Name, AgentNames.Supervisor, StringComparison.OrdinalIgnoreCase)
                ? CloneWithInstructions(participant, supervisorInstructions)
                : participant)
            .ToList();

        return new AgentFrameworkBlueprint
        {
            WorkflowName = blueprint.WorkflowName,
            StartAgent = AgentHandoffTopology.StartAgent,
            ChatModel = blueprint.ChatModel,
            EmbeddingModel = blueprint.EmbeddingModel,
            Participants = participants,
            Handoffs = handoffs
        };
    }

    public Task<bool> ValidateSkillFilesAsync(CancellationToken cancellationToken = default)
    {
        var missing = DescribeBlueprint().Participants
            .Where(participant => !File.Exists(participant.SkillPath))
            .Select(participant => participant.Name)
            .ToList();

        if (missing.Count > 0)
        {
            _logger.LogWarning("Missing skill.md files for agents: {Agents}", string.Join(", ", missing));
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    private static string LoadSupervisorSkill(IReadOnlyList<AgentBlueprint> participants)
    {
        var skillPath = participants
            .FirstOrDefault(participant =>
                string.Equals(participant.Name, AgentNames.Supervisor, StringComparison.OrdinalIgnoreCase))
            ?.SkillPath;

        if (string.IsNullOrWhiteSpace(skillPath) || !File.Exists(skillPath))
        {
            return string.Empty;
        }

        return File.ReadAllText(skillPath);
    }

    private static AgentBlueprint CloneWithInstructions(AgentBlueprint participant, string instructions)
    {
        return new AgentBlueprint
        {
            Name = participant.Name,
            SkillPath = participant.SkillPath,
            Role = participant.Role,
            Instructions = instructions
        };
    }
}

public sealed class AgentFrameworkBlueprint
{
    public string WorkflowName { get; init; } = string.Empty;

    public string StartAgent { get; init; } = string.Empty;

    public string ChatModel { get; init; } = string.Empty;

    public string EmbeddingModel { get; init; } = string.Empty;

    public IReadOnlyList<AgentBlueprint> Participants { get; init; } = [];

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Handoffs { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();
}

public sealed class AgentBlueprint
{
    public string Name { get; init; } = string.Empty;

    public string SkillPath { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string Instructions { get; init; } = string.Empty;
}
