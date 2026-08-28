using System.Text;
using HRMS_CHATBOT_SOURCE.Domain.Constants;

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
        builder.AppendLine("- Hand off only to agents listed under Available skills.");
        builder.AppendLine("- If the user request matches an Unavailable skill, do not hand off and do not perform that action.");
        builder.AppendLine("- Tell the user that capability is not enabled for their account based on the skills available to them, then list Available skills.");
        if (unavailable.Contains(AgentNames.LeaveApplication, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("- Example: if the user asks to apply leave, say you cannot apply leave because LeaveApplicationAgent is not enabled for them.");
        }
        if (unavailable.Contains(AgentNames.Document, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("- Example: if the user asks for any document download, say you that it cannot be done right now as the feature is not enabled for them.");
        }
        if (unavailable.Contains(AgentNames.Knowledge, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("- Example: if the user asks policy/document related questions, say you that it cannot be done right now as the feature is not enabled for them.");
        }

        return builder.ToString().TrimEnd();
    }
}
