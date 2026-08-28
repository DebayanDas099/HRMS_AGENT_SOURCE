using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Request;

public class UpdateAgentGroupActiveRequest
{
    [JsonProperty("agent_id")]
    public long AgentId { get; set; }

    [JsonProperty("user_grp_code")]
    public string UserGrpCode { get; set; } = string.Empty;

    [JsonProperty("user_payroll")]
    public string UserPayroll { get; set; } = "onroll";

    [JsonProperty("is_active")]
    public bool IsActive { get; set; }
}
