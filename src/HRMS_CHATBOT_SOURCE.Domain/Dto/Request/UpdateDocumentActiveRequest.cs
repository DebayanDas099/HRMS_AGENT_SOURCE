using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Request;

public class UpdateDocumentActiveRequest
{
    [JsonProperty("document_id")]
    public long DocumentId { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; }
}
