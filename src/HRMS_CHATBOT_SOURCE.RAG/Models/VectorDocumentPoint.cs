namespace HRMS_CHATBOT_SOURCE.RAG.Models;

public class VectorDocumentPoint
{
    public string PointId { get; set; } = string.Empty;

    public long DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string BlobPath { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public float[] Vector { get; set; } = [];
}
