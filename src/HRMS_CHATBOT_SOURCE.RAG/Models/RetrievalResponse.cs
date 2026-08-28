namespace HRMS_CHATBOT_SOURCE.RAG.Models;

public class RetrievalResponse
{
    public string Query { get; set; } = string.Empty;

    public IReadOnlyList<VectorSearchResult> Results { get; set; } = [];

    /// <summary>
    /// 0-1. Reranker relevance of the top hit when reranking ran, otherwise the
    /// top hit's cosine similarity. Fusion scores are deliberately not used here:
    /// they are rank-derived and carry no absolute meaning.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>True when <see cref="Confidence"/> cleared the configured minimum.</summary>
    public bool IsConfident { get; set; }

    public bool UsedRerank { get; set; }

    public bool UsedLexical { get; set; }

    public int DenseCount { get; set; }

    public int SparseCount { get; set; }

    public int FusedCount { get; set; }

    public long ElapsedMilliseconds { get; set; }
}
