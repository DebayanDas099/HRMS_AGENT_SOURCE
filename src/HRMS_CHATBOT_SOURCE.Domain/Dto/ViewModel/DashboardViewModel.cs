using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.ViewModel;

public class DashboardViewModel
{
    [JsonProperty("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    [JsonProperty("designation")]
    public string Designation { get; set; } = string.Empty;

    [JsonProperty("group_code")]
    public string GroupCode { get; set; } = string.Empty;

    [JsonProperty("total_documents")]
    public int TotalDocuments { get; set; }

    [JsonProperty("policy_documents")]
    public int PolicyDocuments { get; set; }

    [JsonProperty("training_documents")]
    public int TrainingDocuments { get; set; }

    [JsonProperty("active_documents")]
    public int ActiveDocuments { get; set; }
}
