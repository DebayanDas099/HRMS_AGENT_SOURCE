using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class ActiveMobileNumbersResponse
{
    [JsonProperty("mobile_numbers")]
    public IReadOnlyList<string> MobileNumbers { get; set; } = [];
}
