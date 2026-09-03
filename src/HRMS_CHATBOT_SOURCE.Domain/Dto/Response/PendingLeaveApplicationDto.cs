using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class PendingLeaveApplicationDto
{
    [JsonProperty("application_reference")]
    public string ApplicationReference { get; set; } = string.Empty;

    [JsonProperty("mobile")]
    public string Mobile { get; set; } = string.Empty;

    [JsonProperty("employee_name")]
    public string? EmployeeName { get; set; }

    [JsonProperty("from_date")]
    public DateTime FromDate { get; set; }

    [JsonProperty("to_date")]
    public DateTime ToDate { get; set; }

    [JsonProperty("reason")]
    public string? Reason { get; set; }

    [JsonProperty("applied_on")]
    public DateTime AppliedOn { get; set; }
}
