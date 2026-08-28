using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class UserGroupDto
{
    [JsonProperty("grp_user_group_code")]
    public string GroupCode { get; set; } = string.Empty;

    [JsonProperty("grp_user_group_desc")]
    public string GroupDesc { get; set; } = string.Empty;
}
