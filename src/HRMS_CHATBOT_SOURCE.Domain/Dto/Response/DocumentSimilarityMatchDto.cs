using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class DocumentSimilarityMatchDto
{
    [JsonProperty("dm_id")]
    public long DocumentId { get; set; }

    [JsonProperty("dm_category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("dm_name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("dm_path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("dm_active")]
    public string Active { get; set; } = "N";

    [JsonProperty("dm_ingestion_status")]
    public string IngestionStatus { get; set; } = "Pending";

    [JsonProperty("dm_created_date")]
    public DateTime? CreatedDate { get; set; }

    [JsonProperty("similarity_score")]
    public int SimilarityScore { get; set; }
}
