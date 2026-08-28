namespace HRMS_CHATBOT_SOURCE.RAG.Models;

/// <summary>
/// One chunk as written to the Lucene index. The index is a derived cache that can
/// always be rebuilt from the Qdrant payload, which remains the source of truth.
/// </summary>
public class LexicalDocument
{
    public long DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string SectionPath { get; set; } = string.Empty;

    public int StartOffset { get; set; }

    public int EndOffset { get; set; }

    public bool IsActive { get; set; } = true;
}
