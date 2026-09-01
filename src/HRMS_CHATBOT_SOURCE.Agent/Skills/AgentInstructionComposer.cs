namespace HRMS_CHATBOT_SOURCE.Agent.Skills;

public static class AgentInstructionComposer
{
    private const string ReferenceTimeZoneId = "India Standard Time";

    public static string PrependRuntimeContext(string skillMarkdown, DateTime referenceDate)
    {
        var preamble =
                        $"Current Date: {referenceDate:yyyy-MM-dd HH:mm:ss} ({ReferenceTimeZoneId})"
                        + Environment.NewLine + Environment.NewLine
                        + "## Date handling"
                        + Environment.NewLine
                        + "- Use the Current Date above as the anchor for relative phrases."
                        + Environment.NewLine
                        + "- When a parsed date context notice is present in this turn, use those start/end dates."
                        + Environment.NewLine
                        + "- Never invent calendar dates."
                        + Environment.NewLine
                        + "- If no date is mentioned, omit date parameters or use the current month."
                        + Environment.NewLine + Environment.NewLine;

        return preamble + (skillMarkdown?.TrimStart() ?? string.Empty);
    }
}
