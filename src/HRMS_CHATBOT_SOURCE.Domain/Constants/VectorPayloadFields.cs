namespace HRMS_CHATBOT_SOURCE.Domain.Constants;

/// <summary>
/// Payload/field keys shared by the Qdrant collection and the Lucene lexical index.
/// Both engines must agree on these names or fused results cannot be joined.
/// </summary>
public static class VectorPayloadFields
{
    public const string DocumentId = "document_id";
    public const string ChunkIndex = "chunk_index";
    public const string Category = "category";
    public const string Title = "title";
    public const string Content = "content";
    public const string BlobPath = "blob_path";
    public const string IsActive = "is_active";
    public const string SectionPath = "section_path";
    public const string StartOffset = "start_offset";
    public const string EndOffset = "end_offset";

    /// <summary>
    /// Lucene-only composite key of document id and chunk index, used to join
    /// lexical hits against dense hits during fusion.
    /// </summary>
    public const string DocumentKey = "doc_key";

    public static string BuildDocumentKey(long documentId, int chunkIndex)
    {
        return $"{documentId}:{chunkIndex}";
    }
}
