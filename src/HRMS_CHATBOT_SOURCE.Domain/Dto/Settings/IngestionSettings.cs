using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class IngestionSettings
{
    public const string SectionName = "Ingestion";

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonProperty("chunk_size")]
    public int ChunkSize { get; set; } = 1000;

    [JsonProperty("chunk_overlap_ratio")]
    public double ChunkOverlapRatio { get; set; } = 0.15;

    [JsonProperty("batch_size")]
    public int BatchSize { get; set; } = 16;
}
