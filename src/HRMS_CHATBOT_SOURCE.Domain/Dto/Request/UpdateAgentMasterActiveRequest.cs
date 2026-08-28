using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Request;

public class UpdateAgentMasterActiveRequest
{
    [JsonProperty("agent_id")]
    public long AgentId { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; }
}
