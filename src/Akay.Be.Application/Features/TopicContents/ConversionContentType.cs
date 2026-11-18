namespace Akay.Be.Application.Features.TopicContents;

internal static class ConversionContentType
{
    public static string? FromFileName(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" or ".docx" or ".pptx" or ".html" or ".htm" or ".txt" or ".md" => "document",
        ".mp3" or ".wav" or ".aac" or ".m4a" or ".ogg" or ".opus" or ".flac" => "audio",
        ".mp4" or ".mov" or ".mkv" or ".webm" or ".avi" or ".m4v" or ".mpg" or ".mpeg" or ".wmv" => "video",
        _ => null
    };
}
