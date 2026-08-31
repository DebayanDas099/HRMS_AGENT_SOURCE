namespace HRMS_CHATBOT_SOURCE.RAG.Models;

public class VectorSearchResult
{
    public long DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>Heading breadcrumb from the chunker, used for citation context.</summary>
    public string SectionPath { get; set; } = string.Empty;

    public float Score { get; set; }

    /// <summary>Which retrievers surfaced this chunk: "dense", "lexical", or "hybrid".</summary>
    public string MatchedBy { get; set; } = string.Empty;
}
