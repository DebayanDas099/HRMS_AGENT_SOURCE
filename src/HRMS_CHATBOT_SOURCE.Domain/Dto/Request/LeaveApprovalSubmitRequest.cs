using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Request;

public class LeaveApprovalDecisionDto
{
    [JsonProperty("application_reference")]
    public string ApplicationReference { get; set; } = string.Empty;

    /// <summary>"Approved" or "Rejected" - the two only valid values for a submitted decision.</summary>
    [JsonProperty("decision")]
    public string Decision { get; set; } = string.Empty;

    /// <summary>
    /// Optional when approving, required when rejecting - enforced client-side (the
    /// grid won't let a Rejected row be submitted without one) and re-checked here
    /// server-side, since a client-side-only check is not a real guarantee.
    /// </summary>
    [JsonProperty("remarks")]
    public string? Remarks { get; set; }
}

public class LeaveApprovalSubmitRequest
{
    [JsonProperty("decisions")]
    public List<LeaveApprovalDecisionDto> Decisions { get; set; } = [];
}
