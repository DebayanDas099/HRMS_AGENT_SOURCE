namespace HRMS_CHATBOT_SOURCE.Domain.Constants;

public static class AgentNames
{
    public const string Supervisor = "SupervisorAgent";
    public const string LeaveApplication = "LeaveApplicationAgent";
    public const string Document = "DocumentAgent";
    public const string Knowledge = "KnowledgeAgent";

    /// <summary>
    /// Never granted by <c>IAgentAccessService.GetEnabledAgentNamesAsync</c> - this
    /// name is added to the enabled-agent list only by <c>ChatLogic</c>, and only
    /// after a server-verified admin claim (not a client-supplied header) is present
    /// on the request. See ChatLogic.SendMessageAsync.
    /// </summary>
    public const string LeaveApproval = "LeaveApprovalAgent";

    /// <summary>
    /// Gates voice/mic input for chat. Configured via agent_master + agent_appl_user_grp
    /// like other agents, but never included in the handoff workflow.
    /// </summary>
    public const string VoiceInput = "VoiceInputAgent";
}
