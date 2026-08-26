using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class DocumentMstrDto
{
    [JsonProperty("dm_id")]
    public long DocumentId { get; set; }

    [JsonProperty("dm_category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("dm_name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("dm_path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("dm_created_by")]
    public string CreatedBy { get; set; } = string.Empty;

    [JsonProperty("dm_created_date")]
    public DateTime CreatedDate { get; set; }

    [JsonProperty("dm_active")]
    public string Active { get; set; } = "Y";

    [JsonProperty("dm_ingestion_status")]
    public string IngestionStatus { get; set; } = "Pending";

    [JsonProperty("dm_ingested_at")]
    public DateTime? IngestedAt { get; set; }

    [JsonProperty("dm_ingestion_error")]
    public string? IngestionError { get; set; }

    [JsonProperty("dm_chunk_count")]
    public int? ChunkCount { get; set; }
}
