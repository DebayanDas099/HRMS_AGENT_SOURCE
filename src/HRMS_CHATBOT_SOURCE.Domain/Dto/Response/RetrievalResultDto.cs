using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class RetrievalResultDto
{
    [JsonProperty("query")]
    public string Query { get; set; } = string.Empty;

    [JsonProperty("confidence")]
    public double Confidence { get; set; }

    [JsonProperty("is_confident")]
    public bool IsConfident { get; set; }

    [JsonProperty("used_rerank")]
    public bool UsedRerank { get; set; }

    [JsonProperty("used_lexical")]
    public bool UsedLexical { get; set; }

    [JsonProperty("dense_count")]
    public int DenseCount { get; set; }

    [JsonProperty("sparse_count")]
    public int SparseCount { get; set; }

    [JsonProperty("fused_count")]
    public int FusedCount { get; set; }

    [JsonProperty("elapsed_ms")]
    public long ElapsedMilliseconds { get; set; }

    [JsonProperty("results")]
    public List<RetrievalHitDto> Results { get; set; } = [];
}

public class RetrievalHitDto
{
    [JsonProperty("document_id")]
    public long DocumentId { get; set; }

    [JsonProperty("chunk_index")]
    public int ChunkIndex { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; } = string.Empty;

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("section_path")]
    public string SectionPath { get; set; } = string.Empty;

    [JsonProperty("content")]
    public string Content { get; set; } = string.Empty;

    [JsonProperty("score")]
    public float Score { get; set; }

    [JsonProperty("matched_by")]
    public string MatchedBy { get; set; } = string.Empty;
}
