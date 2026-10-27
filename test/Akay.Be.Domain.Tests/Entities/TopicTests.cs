using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;

namespace Akay.Be.Domain.Tests.Entities;

public class TopicTests
{
    [Fact]
    public void Create_InvalidSubjectId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Topic.Create(0, "Topic", null));
        Assert.Contains("subject", ex.Message.ToLower());
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Topic.Create(1, " ", null));
        Assert.Contains("name", ex.Message.ToLower());
    }

    [Fact]
    public void Create_Valid_SetsProperties()
    {
        var topic = Topic.Create(1, "Topic", "Description");

        Assert.Equal(1, topic.SubjectId);
        Assert.Equal("Topic", topic.Name);
        Assert.Equal("Description", topic.Description);
        Assert.Equal(TopicProcessingState.Draft, topic.ProcessingState);
        Assert.Equal(TopicIndexingState.NotIndexed, topic.IndexingState);
        Assert.False(topic.IsActive);
        Assert.Empty(topic.Contents);
    }

    [Fact]
    public void AddContent_Valid_AddsContent()
    {
        var topic = Topic.Create(1, "Topic", null);

        var content = topic.AddContent("lesson.pdf", "application/pdf", "https://original", "https://processed");

        var added = Assert.Single(topic.Contents);
        Assert.Same(content, added);
        Assert.Equal("lesson.pdf", added.FileName);
        Assert.Equal("application/pdf", added.ContentType);
        Assert.Equal("https://original", added.OriginalContentUrl);
        Assert.Equal("https://processed", added.ContentUrl);
        Assert.Equal(ContentProcessingState.UploadPending, added.ProcessingState);
        Assert.Equal(ContentIndexingState.NotIndexed, added.IndexingState);
        Assert.Equal(0, added.ProcessingAttemptId);
    }

    [Fact]
    public void AddContent_EmptyFileName_Throws()
    {
        var topic = Topic.Create(1, "Topic", null);

        var ex = Assert.Throws<ArgumentException>(() =>
            topic.AddContent(" ", "application/pdf", null, null));
        Assert.Contains("filename", ex.Message.ToLower());
        Assert.Empty(topic.Contents);
    }

    [Fact]
    public void AddContent_EmptyContentType_Throws()
    {
        var topic = Topic.Create(1, "Topic", null);

        var ex = Assert.Throws<ArgumentException>(() =>
            topic.AddContent("lesson.pdf", " ", null, null));
        Assert.Contains("contenttype", ex.Message.ToLower());
        Assert.Empty(topic.Contents);
    }

    [Fact]
    public void DeleteContent_Existing_RemovesContent()
    {
        var topic = Topic.Create(1, "Topic", null);
        var content = topic.AddContent("lesson.pdf", "application/pdf", null, null);

        topic.DeleteContent(content);

        Assert.Empty(topic.Contents);
    }

    [Fact]
    public void DeleteContent_NotBelonging_Throws()
    {
        var topic = Topic.Create(1, "Topic", null);
        var otherTopic = Topic.Create(1, "Other", null);
        var content = otherTopic.AddContent("lesson.pdf", "application/pdf", null, null);

        var ex = Assert.Throws<InvalidOperationException>(() => topic.DeleteContent(content));
        Assert.Contains("belong", ex.Message.ToLower());
    }

    [Fact]
    public void ChangeName_Updates()
    {
        var topic = Topic.Create(1, "Topic", null);
        topic.ChangeName("New topic");

        Assert.Equal("New topic", topic.Name);
    }

    [Fact]
    public void ChangeName_Empty_Throws()
    {
        var topic = Topic.Create(1, "Topic", null);

        var ex = Assert.Throws<ArgumentException>(() => topic.ChangeName(" "));
        Assert.Contains("name", ex.Message.ToLower());
    }

    [Fact]
    public void ChangeDescription_Updates()
    {
        var topic = Topic.Create(1, "Topic", "Old");
        topic.ChangeDescription("New");

        Assert.Equal("New", topic.Description);
    }

    [Fact]
    public void ChangeDescription_ToNull_Clears()
    {
        var topic = Topic.Create(1, "Topic", "Old");
        topic.ChangeDescription(null);

        Assert.Null(topic.Description);
    }
}
