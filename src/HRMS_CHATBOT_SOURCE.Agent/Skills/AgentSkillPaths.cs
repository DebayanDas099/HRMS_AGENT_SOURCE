namespace HRMS_CHATBOT_SOURCE.Agent.Skills;

/// <summary>
/// Resolves skill.md instruction files for Microsoft Agent Framework agents.
/// </summary>
public static class AgentSkillPaths
{
    public const string SkillsRoot = "Skills";

    public static string GetSkillPath(string agentName)
    {
        return Path.Combine(AppContext.BaseDirectory, SkillsRoot, agentName, "skill.md");
    }

    public static async Task<string?> LoadSkillAsync(string agentName, CancellationToken cancellationToken = default)
    {
        var skillPath = GetSkillPath(agentName);
        if (!File.Exists(skillPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(skillPath, cancellationToken);
    }
}
