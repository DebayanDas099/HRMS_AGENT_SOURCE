using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class DocumentStatisticsDto
{
    [JsonProperty("total_documents")]
    public int TotalDocuments { get; set; }

    [JsonProperty("policy_documents")]
    public int PolicyDocuments { get; set; }

    [JsonProperty("training_documents")]
    public int TrainingDocuments { get; set; }

    [JsonProperty("active_documents")]
    public int ActiveDocuments { get; set; }

    [JsonProperty("inactive_documents")]
    public int InactiveDocuments { get; set; }
}
