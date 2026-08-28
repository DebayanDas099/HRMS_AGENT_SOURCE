using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class AgentMasterListResponseDto
{
    [JsonProperty("total_agents")]
    public int TotalAgents { get; set; }

    [JsonProperty("active_agents")]
    public int ActiveAgents { get; set; }

    [JsonProperty("inactive_agents")]
    public int InactiveAgents { get; set; }

    [JsonProperty("items")]
    public List<AgentMasterDto> Items { get; set; } = [];
}
