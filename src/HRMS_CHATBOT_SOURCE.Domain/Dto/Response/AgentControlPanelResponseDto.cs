using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class AgentControlPanelResponseDto
{
    [JsonProperty("user_grp_code")]
    public string UserGrpCode { get; set; } = string.Empty;

    [JsonProperty("total_groups")]
    public int TotalGroups { get; set; }

    [JsonProperty("total_agents")]
    public int TotalAgents { get; set; }

    [JsonProperty("active_agents")]
    public int ActiveAgents { get; set; }

    [JsonProperty("inactive_agents")]
    public int InactiveAgents { get; set; }

    [JsonProperty("items")]
    public List<AgentGroupAssignmentDto> Items { get; set; } = [];
}
