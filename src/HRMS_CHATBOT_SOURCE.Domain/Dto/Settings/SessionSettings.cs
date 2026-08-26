using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class SessionSettings
{
    public const string SectionName = "Session";

    [JsonProperty("idle_timeout_minutes")]
    public int IdleTimeoutMinutes { get; set; } = 30;

    [JsonProperty("cookie_name")]
    public string CookieName { get; set; } = "HRMS.Session";

    [JsonProperty("cookie_http_only")]
    public bool CookieHttpOnly { get; set; } = true;
}
