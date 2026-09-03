using HRMS_CHATBOT_SOURCE.Agent;
using HRMS_CHATBOT_SOURCE.Agent.Skills;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Domain.Dto.Response;
using HRMS_CHATBOT_SOURCE.Logic.Common;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

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
        Assert.Contains("source of truth for this turn", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cannot apply leave", instructions, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyCurrentAccessOverride_InsertsNoticeBeforeLatestUserMessage()
    {
        var history = new List<ChatMessage>
        {
            new(ChatRole.User, "I want to apply leave"),
            new(ChatRole.Assistant, "That feature is not available."),
            new(ChatRole.User, "I want to apply leave")
        };

        var runtime = new HrmsChatRuntime(
            null!,
            null!,
            null!,
            null!,
            null!,
            NullLogger<HrmsChatRuntime>.Instance);
        var commonLogic = new CommonLogic();
        var turnMessages = runtime.ApplyCurrentAccessOverride(
            history,
            [AgentNames.Supervisor, AgentNames.LeaveApplication, AgentNames.Document],
            commonLogic,
            authenticatedMobile: "1234567890");

        Assert.Equal(4, turnMessages.Count);
        Assert.Equal(ChatRole.System, turnMessages[2].Role);
        Assert.Contains(AgentNames.LeaveApplication, turnMessages[2].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Available", turnMessages[2].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1234567890", turnMessages[2].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ChatRole.User, turnMessages[3].Role);
        Assert.Equal("I want to apply leave", turnMessages[3].Text);
        Assert.Equal("That feature is not available.", turnMessages[1].Text);
    }

    [Fact]
    public void BuildAuthenticatedEmployeeNotice_IncludesMobile()
    {
        var notice = SupervisorSkillComposer.BuildAuthenticatedEmployeeNotice("1234567890");

        Assert.Contains("1234567890", notice);
        Assert.Contains("do not ask", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildParsedDateNotice_MultipleRanges_InstructsOneLeaveCallPerRange()
    {
        var commonLogic = new StubDateLogic(new RelativeDateParseResult
        {
            Found = true,
            Phrase = "how many leaves of current and last month do i have ?",
            Message = "Resolved 2 date ranges. Call leave balance once per range.",
            Ranges =
            [
                new RelativeDateRangeDto
                {
                    StartDate = "2026-08-01",
                    EndDate = "2026-08-31",
                    Label = "August 2026"
                },
                new RelativeDateRangeDto
                {
                    StartDate = "2026-09-01",
                    EndDate = "2026-09-30",
                    Label = "September 2026"
                }
            ]
        });

        var notice = SupervisorSkillComposer.BuildParsedDateNotice(
            "how many leaves of current and last month do i have ?",
            commonLogic);

        Assert.NotNull(notice);
        Assert.Contains("2026-08-01 to 2026-08-31", notice);
        Assert.Contains("2026-09-01 to 2026-09-30", notice);
        Assert.Contains("once per range", notice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not merge", notice, StringComparison.OrdinalIgnoreCase);
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

        Assert.DoesNotContain(
            HrmsHandoffWorkflowFactory.GetSupervisorHandoffTargets(filtered),
            target => string.Equals(target, AgentNames.LeaveApplication, StringComparison.OrdinalIgnoreCase));
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

    private sealed class StubDateLogic : ICommonLogic
    {
        private readonly RelativeDateParseResult _result;

        public StubDateLogic(RelativeDateParseResult result)
        {
            _result = result;
        }

        public DateTime GetReferenceDateTime(DateTime? utcNow = null) => DateTime.UtcNow;

        public RelativeDateParseResult ParseRelativeDate(string? phrase, DateTime? referenceDate = null)
            => _result;

        public RelativeDateParseResult ParseRelativeDateFromUserMessage(string? message, DateTime? referenceDate = null)
            => _result;

        public Task<string?> GetUserEmailByMobileAsync(string? mobile, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<int> SendMailNewAsync(
            string toAddress,
            string mailSubject,
            string mailBody,
            string? attachmentPath = null,
            string? ccAddress = null,
            string? bccAddress = null,
            string? fromAddress = null,
            string? senderApp = null,
            string? senderTask = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
