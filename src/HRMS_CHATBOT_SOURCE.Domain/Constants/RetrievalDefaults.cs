namespace HRMS_CHATBOT_SOURCE.Domain.Constants;

/// <summary>
/// Defaults for hybrid retrieval. Candidate counts intentionally over-fetch well
/// beyond TopK so the dense and lexical rankings have room to disagree before fusion.
/// </summary>
public static class RetrievalDefaults
{
    public const int TopK = 5;
    public const int MaxTopK = 50;

    public const int DenseCandidates = 50;
    public const int SparseCandidates = 50;
    public const int RerankCandidates = 20;

    /// <summary>Reciprocal Rank Fusion damping constant.</summary>
    public const int RrfK = 60;

    public const double Bm25K1 = 1.2;
    public const double Bm25B = 0.75;

    public const float TitleBoost = 2.0f;
    public const float SectionPathBoost = 1.5f;
    public const float ContentBoost = 1.0f;

    public const int RerankTimeoutSeconds = 8;

    /// <summary>Weight given to reranker relevance when blending with the fusion score.</summary>
    public const double RerankWeight = 0.7;

    /// <summary>Maximum characters of a chunk sent to the reranker, to bound token cost.</summary>
    public const int RerankSnippetLength = 600;

    /// <summary>
    /// Below this, the Knowledge Agent should decline rather than answer from weak context.
    /// </summary>
    public const double MinimumConfidence = 0.45;

    public const string LuceneIndexPath = "App_Data/lucene-index";
}
