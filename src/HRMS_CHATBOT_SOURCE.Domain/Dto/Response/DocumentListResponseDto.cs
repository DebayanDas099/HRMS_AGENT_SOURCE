using Newtonsoft.Json;

namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class DocumentListResponseDto
{
    [JsonProperty("items")]
    public List<DocumentMstrDto> Items { get; set; } = [];

    [JsonProperty("total_count")]
    public int TotalCount { get; set; }

    [JsonProperty("page_number")]
    public int PageNumber { get; set; }

    [JsonProperty("page_size")]
    public int PageSize { get; set; }

    [JsonProperty("total_pages")]
    public int TotalPages { get; set; }
}
