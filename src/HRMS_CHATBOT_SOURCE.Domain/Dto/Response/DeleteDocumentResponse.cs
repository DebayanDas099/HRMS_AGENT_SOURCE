using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class DeleteDocumentResponse
{
    [JsonProperty("document_id")]
    public long DocumentId { get; set; }

    [JsonProperty("blob_deleted")]
    public bool BlobDeleted { get; set; }

    [JsonProperty("vectors_deleted")]
    public bool VectorsDeleted { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;
}
