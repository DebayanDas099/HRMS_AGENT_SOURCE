using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class LogoutResponse
{
    [JsonProperty("response_message")]
    public string ResponseMessage { get; set; } = "Logged out successfully.";
}
