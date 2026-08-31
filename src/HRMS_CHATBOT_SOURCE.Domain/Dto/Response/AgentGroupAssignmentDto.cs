using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class AgentGroupAssignmentDto
{
    [JsonProperty("am_id")]
    public long AgentId { get; set; }

    [JsonProperty("am_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonProperty("am_active")]
    public string MasterActive { get; set; } = "N";

    [JsonProperty("user_payroll")]
    public string UserPayroll { get; set; } = string.Empty;

    [JsonProperty("aaug_active")]
    public string GroupActive { get; set; } = "N";

    [JsonProperty("is_locked")]
    public string IsLocked { get; set; } = "N";

    [JsonProperty("can_toggle")]
    public string CanToggle { get; set; } = "N";

    [JsonProperty("role")]
    public string Role { get; set; } = "Specialist";
}
