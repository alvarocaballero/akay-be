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

    [Fact]
    public void Create_DefaultDisplayOrder_IsOne()
    {
        var topic = Topic.Create(1, "Topic", null);

        Assert.Equal(1, topic.DisplayOrder);
    }

    [Fact]
    public void Create_WithDisplayOrder_SetsDisplayOrder()
    {
        var topic = Topic.Create(1, "Topic", null, 4);

        Assert.Equal(4, topic.DisplayOrder);
    }

    [Fact]
    public void Create_InvalidDisplayOrder_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => Topic.Create(1, "Topic", null, 0));

        Assert.Contains("displayorder", ex.Message.ToLower());
    }

    [Fact]
    public void ChangeDisplayOrder_Updates()
    {
        var topic = Topic.Create(1, "Topic", null);
        topic.ChangeDisplayOrder(7);

        Assert.Equal(7, topic.DisplayOrder);
    }

    [Fact]
    public void ChangeDisplayOrder_Invalid_Throws()
    {
        var topic = Topic.Create(1, "Topic", null);

        Assert.Throws<ArgumentException>(() => topic.ChangeDisplayOrder(0));
    }

    [Theory]
    [InlineData(TopicProcessingState.Draft)]
    [InlineData(TopicProcessingState.Processing)]
    [InlineData(TopicProcessingState.Ready)]
    [InlineData(TopicProcessingState.Failed)]
    public void ChangeProcessingState_Valid_Updates(TopicProcessingState state)
    {
        var topic = Topic.Create(1, "Topic", null);
        topic.ChangeProcessingState(state);

        Assert.Equal(state, topic.ProcessingState);
    }

    [Fact]
    public void ChangeProcessingState_Invalid_Throws()
    {
        var topic = Topic.Create(1, "Topic", null);

        Assert.Throws<ArgumentException>(() => topic.ChangeProcessingState((TopicProcessingState)99));
    }

    [Theory]
    [InlineData(TopicIndexingState.NotIndexed)]
    [InlineData(TopicIndexingState.Pending)]
    [InlineData(TopicIndexingState.Indexing)]
    [InlineData(TopicIndexingState.Indexed)]
    [InlineData(TopicIndexingState.Failed)]
    public void ChangeIndexingState_Valid_Updates(TopicIndexingState state)
    {
        var topic = Topic.Create(1, "Topic", null);
        topic.ChangeIndexingState(state);

        Assert.Equal(state, topic.IndexingState);
    }

    [Fact]
    public void ChangeIndexingState_Invalid_Throws()
    {
        var topic = Topic.Create(1, "Topic", null);

        Assert.Throws<ArgumentException>(() => topic.ChangeIndexingState((TopicIndexingState)99));
    }

    [Fact]
    public void ChangeActivation_Updates()
    {
        var topic = Topic.Create(1, "Topic", null);

        topic.ChangeActivation(true);
        Assert.True(topic.IsActive);

        topic.ChangeActivation(false);
        Assert.False(topic.IsActive);
    }
}
