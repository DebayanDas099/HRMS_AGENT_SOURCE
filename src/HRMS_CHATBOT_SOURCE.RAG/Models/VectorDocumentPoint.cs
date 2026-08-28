namespace HRMS_CHATBOT_SOURCE.RAG.Models;

public class VectorDocumentPoint
{
    public long DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string BlobPath { get; set; } = string.Empty;

    public string SectionPath { get; set; } = string.Empty;

    public int StartOffset { get; set; }

    public int EndOffset { get; set; }

    public bool IsActive { get; set; } = true;

    public float[] Vector { get; set; } = [];
}
