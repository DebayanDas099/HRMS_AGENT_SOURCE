namespace HRMS_CHATBOT_SOURCE.Domain.Constants;

public static class DocumentUploadConstants
{
    public const long MaxFileSizeBytes = 209_715_200; // 200 MB

    public static readonly string[] AllowedExtensions =
    [
        ".pdf", ".doc", ".docx", ".txt", ".ppt", ".pptx", ".xls", ".xlsx", ".csv", ".md",
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg",
        ".mp4", ".mov", ".avi", ".wmv", ".mkv", ".webm", ".m4v", ".mpeg", ".mpg"
    ];

    public static string AcceptAttribute => string.Join(',', AllowedExtensions);
}
