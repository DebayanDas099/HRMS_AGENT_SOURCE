namespace HRMS_CHATBOT_SOURCE.RAG.Models;

/// <summary>
/// A chunk surfaced by one or both retrievers, carrying each engine's rank so
/// fusion can reason about them independently.
/// </summary>
public class RetrievalCandidate
{
    public string DocumentKey { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string SectionPath { get; set; } = string.Empty;

    /// <summary>1-based rank from the dense retriever, null when it did not return this chunk.</summary>
    public int? DenseRank { get; set; }

    /// <summary>Cosine similarity, only meaningful when <see cref="DenseRank"/> is set.</summary>
    public float? DenseScore { get; set; }

    /// <summary>1-based rank from the BM25 retriever, null when it did not return this chunk.</summary>
    public int? SparseRank { get; set; }

    /// <summary>Raw BM25 score; unbounded and corpus-dependent, so never compare it to a cosine.</summary>
    public float? SparseScore { get; set; }

    public double FusedScore { get; set; }

    /// <summary>Reranker relevance on a 0-10 scale, null when reranking did not run.</summary>
    public double? RerankScore { get; set; }

    public double FinalScore { get; set; }
}
