using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Settings;

public class CorsSettings
{
    public const string SectionName = "Cors";

    [JsonProperty("allowed_origins")]
    public string[] AllowedOrigins { get; set; } = [];

    [JsonProperty("allowed_methods")]
    public string[] AllowedMethods { get; set; } = ["GET", "POST", "PUT", "DELETE", "OPTIONS"];

    [JsonProperty("allowed_headers")]
    public string[] AllowedHeaders { get; set; } = ["*"];

    [JsonProperty("allow_credentials")]
    public bool AllowCredentials { get; set; } = true;
}
