namespace HRMS_CHATBOT_SOURCE.Domain.Helpers;

public static class DocumentFileHelper
{
    public static string ExtractFileName(string blobPath)
    {
        var blobName = blobPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? blobPath;
        var underscoreIndex = blobName.IndexOf('_');
        return underscoreIndex >= 0 && underscoreIndex < blobName.Length - 1
            ? blobName[(underscoreIndex + 1)..]
            : blobName;
    }

    public static string ResolveMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".csv" => "text/csv",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".wmv" => "video/x-ms-wmv",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".m4v" => "video/x-m4v",
            ".mpeg" or ".mpg" => "video/mpeg",
            _ => "application/octet-stream"
        };
    }
}
