using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Request;

public class DeleteDocumentRequest
{
    [JsonProperty("document_id")]
    public long DocumentId { get; set; }
}
