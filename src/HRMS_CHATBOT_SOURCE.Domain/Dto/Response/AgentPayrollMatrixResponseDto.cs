using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class AgentPayrollMatrixResponseDto
{
    [JsonProperty("am_id")]
    public long AgentId { get; set; }

    [JsonProperty("am_name")]
    public string AgentName { get; set; } = string.Empty;

    [JsonProperty("items")]
    public List<AgentPayrollAssignmentDto> Items { get; set; } = [];
}
