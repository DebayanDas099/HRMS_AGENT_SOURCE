using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class DocumentUploadResultItem
{
    [JsonProperty("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("document")]
    public DocumentMstrDto? Document { get; set; }

    [JsonProperty("ingestion")]
    public IngestionResultDto? Ingestion { get; set; }
}
