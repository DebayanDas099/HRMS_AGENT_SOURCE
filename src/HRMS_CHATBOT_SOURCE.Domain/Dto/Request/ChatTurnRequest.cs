using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Request;

public class ChatTurnRequest
{
    [JsonProperty("mobile")]
    public string? Mobile { get; set; }

    [Required(ErrorMessage = "Message is required.")]
    [JsonProperty("message")]
    public string Message { get; set; } = "Apply leave tomorrow";

    [JsonProperty("conversation_id")]
    public string? ConversationId { get; set; }
}
