using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Request;

public class LoginRequest
{
    [Required(ErrorMessage = "User ID is required")]
    [Display(Name = "User ID")]
    [JsonProperty("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    [JsonProperty("password")]
    public string Password { get; set; } = string.Empty;

    [JsonProperty("remember_me")]
    public bool RememberMe { get; set; }
}
