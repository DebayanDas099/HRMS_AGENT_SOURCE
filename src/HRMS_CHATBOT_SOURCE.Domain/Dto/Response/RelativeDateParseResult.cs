namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public sealed class RelativeDateParseResult
{
    public bool Found { get; init; }
    public string Phrase { get; init; } = string.Empty;
    public string? StartDate { get; init; }
    public string? EndDate { get; init; }
    public string? Type { get; init; }
    public string? Message { get; init; }
}
