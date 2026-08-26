using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class ErrorResponse
{
    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("error_code")]
    public string? ErrorCode { get; set; }
}
