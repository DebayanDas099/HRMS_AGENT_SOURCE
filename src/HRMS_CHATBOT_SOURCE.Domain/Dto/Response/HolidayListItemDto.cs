using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class HolidayListItemDto
{
    [JsonProperty("holiday_date")]
    public DateTime HolidayDate { get; set; }

    [JsonProperty("holiday_name")]
    public string HolidayName { get; set; } = string.Empty;

    [JsonProperty("holiday_type")]
    public string? HolidayType { get; set; }
}
