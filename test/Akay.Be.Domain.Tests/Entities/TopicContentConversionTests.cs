using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;
using Akay.Be.Domain.Events.Academic;

namespace Akay.Be.Domain.Tests.Entities;

public class TopicContentConversionTests
{
    [Fact]
    public void UploadCreatesCorrelatedEventAndConversionRequiresSameRequest()
    {
        var topic = Topic.Create(1, "Topic");
        var content = topic.AddContent("file.pdf", "application/pdf");
        topic.CompleteContentUpload(content, "https://storage.example/file", 12);

        var request = Assert.IsType<TopicContentUploadCompleted>(Assert.Single(topic.BeforeSaveDomainEvents));
        Assert.NotEqual(Guid.Empty, request.RequestId);
        Assert.Equal(request.RequestId, content.RequestId);
        Assert.Equal(12, request.UserId);
        Assert.Equal(ContentProcessingState.Pending, content.ProcessingState);
        Assert.Throws<InvalidOperationException>(() => topic.CompleteContentConversion(content, Guid.NewGuid(), "https://storage.example/result.md"));
        topic.CompleteContentConversion(content, request.RequestId, "https://storage.example/result.md");
        Assert.Equal(ContentProcessingState.Ready, content.ProcessingState);
        Assert.Throws<InvalidOperationException>(() => topic.FailContentConversion(content, request.RequestId, "error"));
    }

    [Fact]
    public void ForeignContentCannotBeModified()
    {
        var topic = Topic.Create(1, "Topic");
        var foreign = Topic.Create(1, "Other").AddContent("file.pdf", "application/pdf");
        Assert.Throws<InvalidOperationException>(() => topic.CompleteContentUpload(foreign, "https://storage.example/file", 12));
        Assert.Throws<InvalidOperationException>(() => topic.FailContentUpload(foreign, "error"));
        Assert.Throws<InvalidOperationException>(() => topic.FailContentConversion(foreign, Guid.NewGuid(), "error"));
    }
}
