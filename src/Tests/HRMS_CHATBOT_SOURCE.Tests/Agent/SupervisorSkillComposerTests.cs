using HRMS_CHATBOT_SOURCE.Agent;
using HRMS_CHATBOT_SOURCE.Agent.Skills;
using HRMS_CHATBOT_SOURCE.Domain.Constants;

namespace HRMS_CHATBOT_SOURCE.Tests.Agent;

public class SupervisorSkillComposerTests
{
    [Fact]
    public void Compose_WhenLeaveIsDisabled_TellsSupervisorItCannotApplyLeave()
    {
        var instructions = SupervisorSkillComposer.Compose(
            "# Supervisor Agent",
            [AgentNames.Supervisor, AgentNames.Document, AgentNames.Knowledge]);

        Assert.Contains("`DocumentAgent`", instructions);
        Assert.Contains("`KnowledgeAgent`", instructions);
        Assert.Contains("## Unavailable skills", instructions);
        Assert.Contains("`LeaveApplicationAgent`", instructions);
        Assert.Contains("cannot apply leave", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("- `LeaveApplicationAgent`: leave balance", ExtractSection(instructions, "Available skills"));
    }

    [Fact]
    public void Compose_WhenAllSpecialistsEnabled_HasNoUnavailableSkills()
    {
        var instructions = SupervisorSkillComposer.Compose(
            "# Supervisor Agent",
            [
                AgentNames.Supervisor,
                AgentNames.LeaveApplication,
                AgentNames.Document,
                AgentNames.Knowledge
            ]);

        Assert.Contains("`LeaveApplicationAgent`", ExtractSection(instructions, "Available skills"));
        Assert.Contains("- None.", ExtractSection(instructions, "Unavailable skills"));
        Assert.DoesNotContain("cannot apply leave", instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FilterBlueprint_OmitsDisabledLeaveAgentAndHandoff()
    {
        var source = new AgentFrameworkBlueprint
        {
            WorkflowName = "test",
            StartAgent = AgentNames.Supervisor,
            Participants =
            [
                new AgentBlueprint { Name = AgentNames.Supervisor, Role = "Coordinator" },
                new AgentBlueprint { Name = AgentNames.LeaveApplication, Role = "Specialist" },
                new AgentBlueprint { Name = AgentNames.Document, Role = "Specialist" }
            ],
            Handoffs = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                [AgentNames.Supervisor] = [AgentNames.LeaveApplication, AgentNames.Document],
                [AgentNames.LeaveApplication] = [AgentNames.Supervisor],
                [AgentNames.Document] = [AgentNames.Supervisor]
            }
        };

        var filtered = HandoffWorkflowTemplate.FilterBlueprint(
            source,
            [AgentNames.Supervisor, AgentNames.Document]);

        Assert.DoesNotContain(filtered.Participants, participant =>
            string.Equals(participant.Name, AgentNames.LeaveApplication, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            filtered.Handoffs[AgentNames.Supervisor],
            target => string.Equals(target, AgentNames.LeaveApplication, StringComparison.OrdinalIgnoreCase));

        var supervisor = Assert.Single(
            filtered.Participants,
            participant => string.Equals(participant.Name, AgentNames.Supervisor, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("cannot apply leave", supervisor.Instructions, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractSection(string markdown, string heading)
    {
        var marker = $"## {heading}";
        var start = markdown.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        var afterHeading = start + marker.Length;
        var next = markdown.IndexOf("## ", afterHeading, StringComparison.Ordinal);
        return next < 0 ? markdown[afterHeading..] : markdown[afterHeading..next];
    }
}
