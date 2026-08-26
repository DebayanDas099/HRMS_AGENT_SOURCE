using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class AppSettings
{
    public const string SectionName = "AppSettings";
    public const string JwtAudienceAdmin = "Admin";

    [JsonProperty("admin_private")]
    public string? AdminPrivate { get; set; }

    [JsonProperty("encryption_key")]
    public string? EncryptionKey { get; set; }

    [JsonProperty("jwt_issuer")]
    public string? JwtIssuer { get; set; }

    [JsonProperty("jwt_is_validate_audience")]
    public bool JwtIsValidateAudience { get; set; }

    [JsonProperty("jwt_is_validate_issuer")]
    public bool JwtIsValidateIssuer { get; set; }

    [JsonProperty("jwt_access_token_expiry_in_min")]
    public int JwtAccessTokenExpiryInMin { get; set; } = 480;

    [JsonProperty("remember_me_days")]
    public int RememberMeDays { get; set; } = 7;
}
