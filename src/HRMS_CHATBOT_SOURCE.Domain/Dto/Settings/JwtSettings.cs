using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    [JsonProperty("secret")]
    public string Secret { get; set; } = string.Empty;

    [JsonProperty("issuer")]
    public string Issuer { get; set; } = "HRMS.Chatbot";

    [JsonProperty("audience")]
    public string Audience { get; set; } = "HRMS.Admin";

    [JsonProperty("access_token_minutes")]
    public int AccessTokenMinutes { get; set; } = 480;

    [JsonProperty("remember_me_days")]
    public int RememberMeDays { get; set; } = 7;
}
