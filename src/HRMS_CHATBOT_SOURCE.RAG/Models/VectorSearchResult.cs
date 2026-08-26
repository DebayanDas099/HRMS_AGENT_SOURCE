namespace HRMS_CHATBOT_SOURCE.RAG.Models;

public class VectorSearchResult
{
    public long DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public float Score { get; set; }
}
