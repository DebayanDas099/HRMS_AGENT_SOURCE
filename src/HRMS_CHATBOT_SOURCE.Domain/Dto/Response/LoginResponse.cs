using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class LoginResponse
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonProperty("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty("user")]
    public AdminUserDto User { get; set; } = new();
}

public class AdminUserDto
{
    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    [JsonProperty("designation")]
    public string? Designation { get; set; }

    [JsonProperty("group_code")]
    public string GroupCode { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("mobile")]
    public string? Mobile { get; set; }
}
