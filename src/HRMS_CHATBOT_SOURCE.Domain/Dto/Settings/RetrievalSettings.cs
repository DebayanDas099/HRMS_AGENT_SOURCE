using HRMS_CHATBOT_SOURCE.Domain.Constants;
using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class RetrievalSettings
{
    public const string SectionName = "Retrieval";

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonProperty("lucene_index_path")]
    public string LuceneIndexPath { get; set; } = RetrievalDefaults.LuceneIndexPath;

    [JsonProperty("lexical_enabled")]
    public bool LexicalEnabled { get; set; } = true;

    [JsonProperty("dense_candidates")]
    public int DenseCandidates { get; set; } = RetrievalDefaults.DenseCandidates;

    [JsonProperty("sparse_candidates")]
    public int SparseCandidates { get; set; } = RetrievalDefaults.SparseCandidates;

    [JsonProperty("rerank_candidates")]
    public int RerankCandidates { get; set; } = RetrievalDefaults.RerankCandidates;

    [JsonProperty("top_k")]
    public int TopK { get; set; } = RetrievalDefaults.TopK;

    [JsonProperty("rrf_k")]
    public int RrfK { get; set; } = RetrievalDefaults.RrfK;

    [JsonProperty("bm25_k1")]
    public double Bm25K1 { get; set; } = RetrievalDefaults.Bm25K1;

    [JsonProperty("bm25_b")]
    public double Bm25B { get; set; } = RetrievalDefaults.Bm25B;

    [JsonProperty("title_boost")]
    public float TitleBoost { get; set; } = RetrievalDefaults.TitleBoost;

    [JsonProperty("section_path_boost")]
    public float SectionPathBoost { get; set; } = RetrievalDefaults.SectionPathBoost;

    [JsonProperty("rerank_enabled")]
    public bool RerankEnabled { get; set; } = true;

    [JsonProperty("rerank_timeout_seconds")]
    public int RerankTimeoutSeconds { get; set; } = RetrievalDefaults.RerankTimeoutSeconds;

    [JsonProperty("rerank_weight")]
    public double RerankWeight { get; set; } = RetrievalDefaults.RerankWeight;

    [JsonProperty("minimum_confidence")]
    public double MinimumConfidence { get; set; } = RetrievalDefaults.MinimumConfidence;

    [JsonProperty("auto_rebuild_on_empty_index")]
    public bool AutoRebuildOnEmptyIndex { get; set; } = true;
}
