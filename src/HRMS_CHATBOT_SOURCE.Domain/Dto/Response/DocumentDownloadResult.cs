namespace HRMS_CHATBOT_SOURCE.Domain.Dto.Response;

public class DocumentDownloadResult
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public byte[] FileContent { get; set; } = Array.Empty<byte>();
}
