using System.Text;
using HRMS_CHATBOT_SOURCE.Domain.Constants;
using HRMS_CHATBOT_SOURCE.Logic.Common;

namespace HRMS_CHATBOT_SOURCE.Agent.Skills;

/// <summary>
/// Builds Supervisor instructions so only SQL-enabled specialists are treated as skills.
/// </summary>
public static class SupervisorSkillComposer
{
    public static string Compose(
        string supervisorSkillMarkdown,
        IReadOnlyCollection<string>? enabledAgentNames)
    {
        var enabled = new HashSet<string>(
            enabledAgentNames ?? AgentSkillCatalog.SpecialistAgentNames,
            StringComparer.OrdinalIgnoreCase)
        {
            AgentNames.Supervisor
        };

        var available = AgentSkillCatalog.SpecialistAgentNames
            .Where(enabled.Contains)
            .ToList();
        var unavailable = AgentSkillCatalog.SpecialistAgentNames
            .Where(name => !enabled.Contains(name))
            .ToList();

        var builder = new StringBuilder();
        var baseSkill = supervisorSkillMarkdown?.Trim() ?? string.Empty;
        if (baseSkill.Length > 0)
        {
            builder.AppendLine(baseSkill);
            builder.AppendLine();
        }

        builder.AppendLine("## Available skills");
        if (available.Count == 0)
        {
            builder.AppendLine("- None. Do not hand off to a specialist.");
        }
        else
        {
            foreach (var name in available)
            {
                builder.AppendLine($"- `{name}`: {AgentSkillCatalog.Describe(name)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Unavailable skills");
        if (unavailable.Count == 0)
        {
            builder.AppendLine("- None.");
        }
        else
        {
            foreach (var name in unavailable)
            {
                builder.AppendLine($"- `{name}`: {AgentSkillCatalog.Describe(name)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Skill enforcement");
        builder.AppendLine("- Available and Unavailable skills in these instructions are the source of truth for this turn.");
        builder.AppendLine("- If earlier assistant messages said a feature was not enabled, ignore that when the agent is listed under Available skills now. Hand off using the current list.");
        builder.AppendLine("- Hand off only to agents listed under Available skills.");
        builder.AppendLine("- If the user request matches an Unavailable skill, do not hand off and do not perform that action.");
        builder.AppendLine("- Tell the user that capability is not enabled for their account based on the skills available to them, then list Available skills.");
        if (unavailable.Contains(AgentNames.LeaveApplication, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("- Example: if the user asks to apply leave, say you cannot apply leave because LeaveApplicationAgent is not enabled for them.");
        }
        if (unavailable.Contains(AgentNames.Document, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("- Example: if the user asks for any document download, say you cannot do that because DocumentAgent is not enabled for them.");
        }
        if (unavailable.Contains(AgentNames.Knowledge, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("- Example: if the user asks policy or knowledge-base questions, say you cannot do that because KnowledgeAgent is not enabled for them.");
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildCurrentAccessNotice(IReadOnlyCollection<string>? enabledAgentNames)
    {
        var enabled = new HashSet<string>(
            enabledAgentNames ?? AgentSkillCatalog.SpecialistAgentNames,
            StringComparer.OrdinalIgnoreCase)
        {
            AgentNames.Supervisor
        };

        var available = AgentSkillCatalog.SpecialistAgentNames.Where(enabled.Contains).ToList();
        var unavailable = AgentSkillCatalog.SpecialistAgentNames.Where(name => !enabled.Contains(name)).ToList();

        var builder = new StringBuilder();
        builder.AppendLine("Current agent access for this turn (source of truth):");
        builder.AppendLine(
            "- Available: " + (available.Count == 0 ? "None" : string.Join(", ", available)));
        builder.AppendLine(
            "- Unavailable: " + (unavailable.Count == 0 ? "None" : string.Join(", ", unavailable)));
        builder.Append(
            "Ignore earlier assistant messages that said a feature was not enabled if that agent is in Available now. Hand off using this list, not past refusals.");
        return builder.ToString();
    }

    public static string BuildAuthenticatedEmployeeNotice(string mobile)
    {
        return "Authenticated employee mobile for this session: "
            + mobile.Trim()
            + ". Use this mobile for leave tools and agent lookups; do not ask the user for their mobile number again.";
    }

    public static string? BuildParsedDateNotice(string? userMessage, ICommonLogic commonLogic)
    {
        var parsed = commonLogic.ParseRelativeDateFromUserMessage(userMessage);
        if (!parsed.Found || string.IsNullOrWhiteSpace(parsed.StartDate))
        {
            return null;
        }

        var end = parsed.EndDate ?? parsed.StartDate;
        return "Parsed date context from the user's latest message:"
            + Environment.NewLine
            + $"- Phrase: \"{parsed.Phrase}\""
            + Environment.NewLine
            + $"- Resolved range: {parsed.StartDate} to {end}"
            + Environment.NewLine
            + "- Use this resolved date range for any date-bounded request unless the user corrects it.";
    }
}
