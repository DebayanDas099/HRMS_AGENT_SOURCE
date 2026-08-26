using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class BulkDocumentUploadResponse
{
    [JsonProperty("total_files")]
    public int TotalFiles { get; set; }

    [JsonProperty("success_count")]
    public int SuccessCount { get; set; }

    [JsonProperty("failed_count")]
    public int FailedCount { get; set; }

    [JsonProperty("results")]
    public List<DocumentUploadResultItem> Results { get; set; } = [];
}
