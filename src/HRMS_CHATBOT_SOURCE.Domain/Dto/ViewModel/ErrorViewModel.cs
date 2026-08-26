using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.ViewModel;

public class ErrorViewModel
{
    [JsonProperty("request_id")]
    public string? RequestId { get; set; }

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("error_code")]
    public string? ErrorCode { get; set; }

    [JsonIgnore]
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    [JsonIgnore]
    public bool HasErrorMessage => !string.IsNullOrWhiteSpace(ErrorMessage);
}
