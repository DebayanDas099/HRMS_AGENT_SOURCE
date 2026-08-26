using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class IngestionResultDto
{
    [JsonProperty("document_id")]
    public long DocumentId { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("chunk_count")]
    public int ChunkCount { get; set; }

    [JsonProperty("vector_count")]
    public int VectorCount { get; set; }

    [JsonProperty("collection_name")]
    public string? CollectionName { get; set; }

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }
}
