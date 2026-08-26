using HRMS_CHATBOT_SOURCE.Domain.Dto.Request;
using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.ViewModel;

public class LoginViewModel : LoginRequest
{
    [JsonProperty("return_url")]
    public string? ReturnUrl { get; set; }
}
