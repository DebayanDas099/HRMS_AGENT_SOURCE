using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class AuthenticatedAdminUser
{
    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("last_name")]
    public string? LastName { get; set; }

    [JsonProperty("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonProperty("group_code")]
    public string GroupCode { get; set; } = string.Empty;

    [JsonProperty("department")]
    public string Department { get; set; } = string.Empty;

    [JsonProperty("designation")]
    public string? Designation { get; set; }

    [JsonProperty("employee_id")]
    public string? EmployeeId { get; set; }

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("active")]
    public string Active { get; set; } = "N";

    [JsonProperty("admin_yn")]
    public string? AdminYn { get; set; }

    [JsonProperty("exit_date")]
    public DateTime? ExitDate { get; set; }

    [JsonIgnore]
    public bool IsAdmin => string.Equals(AdminYn, "Y", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsActive => string.Equals(Active, "Y", StringComparison.OrdinalIgnoreCase);
}
