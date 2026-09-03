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
                        + "- When a parsed date context notice is present in this turn, use those dates."
                        + Environment.NewLine
                        + "- If the notice lists one range, use that start and end."
                        + Environment.NewLine
                        + "- If the notice lists several ranges, call GetLeaveStatusAsync once per range with that startDate and endDate. Do not merge ranges into a single leave-balance call."
                        + Environment.NewLine
                        + "- After every leave-balance tool result is back, include every range in the final reply, labeled by month."
                        + Environment.NewLine
                        + "- For applying leave, use one overall from/to (earliest start through latest end). Do not split an apply-leave request across ranges."
                        + Environment.NewLine
                        + "- Before submitting leave, confirm leave type (casual, sick, earned, loss of pay) with the user."
                        + Environment.NewLine
                        + "- Never invent calendar dates."
                        + Environment.NewLine
                        + "- If no date is mentioned, omit date parameters or use the current month."
                        + Environment.NewLine + Environment.NewLine;

        return preamble + (skillMarkdown?.TrimStart() ?? string.Empty);
    }
}
