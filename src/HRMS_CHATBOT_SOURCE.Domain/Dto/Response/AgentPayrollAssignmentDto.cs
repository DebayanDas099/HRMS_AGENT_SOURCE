using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class AgentPayrollAssignmentDto
{
    [JsonProperty("am_id")]
    public long AgentId { get; set; }

    [JsonProperty("am_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonProperty("am_active")]
    public string MasterActive { get; set; } = "N";

    [JsonProperty("grp_user_group_code")]
    public string UserGrpCode { get; set; } = string.Empty;

    [JsonProperty("grp_user_group_desc")]
    public string UserGrpDesc { get; set; } = string.Empty;

    [JsonProperty("user_payroll")]
    public string UserPayroll { get; set; } = "onroll";

    [JsonProperty("aaug_active")]
    public string GroupActive { get; set; } = "N";

    [JsonProperty("is_locked")]
    public string IsLocked { get; set; } = "N";

    [JsonProperty("can_toggle")]
    public string CanToggle { get; set; } = "N";
}
