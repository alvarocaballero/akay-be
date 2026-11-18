using Akay.Be.Application.Features.TopicContents;

namespace Akay.Be.Application.Tests.AdminAcademic;

public class ConversionContentTypeTests
{
    [Theory]
    [InlineData("pdf", "document")]
    [InlineData("docx", "document")]
    [InlineData("pptx", "document")]
    [InlineData("html", "document")]
    [InlineData("htm", "document")]
    [InlineData("txt", "document")]
    [InlineData("md", "document")]
    [InlineData("mp3", "audio")]
    [InlineData("wav", "audio")]
    [InlineData("aac", "audio")]
    [InlineData("m4a", "audio")]
    [InlineData("ogg", "audio")]
    [InlineData("opus", "audio")]
    [InlineData("flac", "audio")]
    [InlineData("mp4", "video")]
    [InlineData("mov", "video")]
    [InlineData("mkv", "video")]
    [InlineData("webm", "video")]
    [InlineData("avi", "video")]
    [InlineData("m4v", "video")]
    [InlineData("mpg", "video")]
    [InlineData("mpeg", "video")]
    [InlineData("wmv", "video")]
    public void SupportedExtensionsAreCaseInsensitive(string extension, string expected)
    {
        Assert.Equal(expected, ConversionContentType.FromFileName($"file.{extension}"));
        Assert.Equal(expected, ConversionContentType.FromFileName($"file.{extension.ToUpperInvariant()}"));
    }

    [Theory]
    [InlineData("file")]
    [InlineData("file.exe")]
    [InlineData("file.pdf.exe")]
    public void UnknownExtensionsAreRejected(string fileName)
    {
        Assert.Null(ConversionContentType.FromFileName(fileName));
    }
}
