using System.Text.Json;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.TopicContents;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;
using Akay.Be.Domain.Events.Academic;
using Akay.To.Core.Application.Abstractions.Outbox;
using Moq;

namespace Akay.Be.Application.Tests.AdminAcademic;

public class SubjectTopicConvertedTests
{
    private const string Source = "subjects/3/topics/5/sources/550e8400-e29b-41d4-a716-446655440000.pdf";
    private const string Destination = "subjects/3/topics/5/content/550e8400-e29b-41d4-a716-446655440000.md";

    [Fact]
    public void IndividualNotificationPayloadContainsOnlyProcessingFields()
    {
        var notification = new FileUploadedUpdated(12, 5, 15, "Ready");
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(notification, JsonSerializerOptions.Web));
        var payload = document.RootElement;
        Assert.Equal(3, payload.EnumerateObject().Count());
        Assert.Equal(5, payload.GetProperty("topicId").GetInt32());
        Assert.Equal(15, payload.GetProperty("topicContentId").GetInt32());
        Assert.Equal("Ready", payload.GetProperty("processingState").GetString());
    }

    [Fact]
    public async Task EventProducesExactConversionContract()
    {
        var writer = new Mock<IOutboxEventWriter>();
        ConversionRequest? actual = null;
        writer.Setup(x => x.Enqueue(It.IsAny<ConversionRequest>())).Callback<Akay.To.Core.Domain.Events.IOutboxDomainEvent>(x => actual = (ConversionRequest)x);
        var id = Guid.NewGuid();
        await new TopicContentUploadCompletedHandler(writer.Object).Handle(new TopicContentUploadCompleted(3, 5, 15, 12, id, "file.PDF", $"https://example.test/topic-content/{Source}"), CancellationToken.None);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(actual));
        var root = document.RootElement;
        Assert.Equal(id, root.GetProperty("requestId").GetGuid());
        Assert.Equal(Source, root.GetProperty("source").GetProperty("blobName").GetString());
        Assert.Equal(Destination, root.GetProperty("destinationBlobName").GetString());
        Assert.Equal("document", root.GetProperty("contentType").GetString());
        Assert.True(root.GetProperty("overwriteDestination").GetBoolean());
        Assert.Equal("topic-content", root.GetProperty("callback").GetString());
        Assert.Equal(12, root.GetProperty("payload").GetProperty("userId").GetInt32());
        Assert.Equal(7, root.EnumerateObject().Count());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TerminalCallbackCorrelatesAndNotifiesOriginalUser(bool failed)
    {
        var (topic, content, request) = CreateAttempt();
        var repository = new Mock<ITopicRepository>();
        repository.Setup(x => x.GetWithContentsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        repository.Setup(x => x.TrySaveConversionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.GetContentCompletionAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new TopicContentCompletion(3, 5, "Topic", failed));
        var notifications = new Mock<INotificationService>();
        var handler = new SubjectTopicConvertedHandler(repository.Object, notifications.Object);
        var callback = request with { EventType = failed ? "document.conversion.failed" : "document.conversion.completed", Error = failed ? new ConversionError("FAIL", "failed") : null };

        Assert.True((await handler.Handle(callback, CancellationToken.None)).IsSuccess);
        Assert.Equal(failed ? ContentProcessingState.Failed : ContentProcessingState.Ready, content.ProcessingState);
        notifications.Verify(x => x.SendAsync(It.Is<FileUploadedUpdated>(n => n.UserId == 12 && n.ProcessingState == (failed ? "Failed" : "Ready")), It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(x => x.SendAsync(It.Is<UploadBatchCompleted>(n => n.UserId == 12 && n.HasErrors == failed), It.IsAny<CancellationToken>()), Times.Once);

        typeof(TopicContent).GetProperty(nameof(TopicContent.UpdatedBy))!.SetValue(content, "999");
        Assert.True((await handler.Handle(callback, CancellationToken.None)).IsSuccess);
        repository.Verify(x => x.TrySaveConversionAsync(It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(x => x.SendAsync(It.Is<FileUploadedUpdated>(n => n.UserId == 12), It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.True((await handler.Handle(callback with { EventType = failed ? "document.conversion.completed" : "document.conversion.failed", Error = new ConversionError("FAIL", "failed") }, CancellationToken.None)).IsSuccess);
        Assert.Equal(failed ? ContentProcessingState.Failed : ContentProcessingState.Ready, content.ProcessingState);
    }

    [Fact]
    public async Task OldAndMismatchedCallbacksCannotModifyContent()
    {
        var (topic, content, request) = CreateAttempt();
        var repository = new Mock<ITopicRepository>();
        repository.Setup(x => x.GetWithContentsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        var handler = new SubjectTopicConvertedHandler(repository.Object, Mock.Of<INotificationService>());
        Assert.True((await handler.Handle(request with { RequestId = Guid.NewGuid() }, CancellationToken.None)).IsSuccess);
        Assert.True((await handler.Handle(request with { Payload = request.Payload! with { userId = 99 } }, CancellationToken.None)).IsFailure);
        Assert.True((await handler.Handle(request with { DestinationBlobName = "elsewhere.md" }, CancellationToken.None)).IsFailure);
        Assert.Equal(ContentProcessingState.Pending, content.ProcessingState);
        repository.Verify(x => x.TrySaveConversionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-user")]
    [InlineData("0")]
    [InlineData("99")]
    public async Task InvalidOrDifferentCreatorCannotReceiveConversion(string? createdBy)
    {
        var (topic, content, request) = CreateAttempt();
        typeof(TopicContent).GetProperty(nameof(TopicContent.CreatedBy))!.SetValue(content, createdBy);
        var repository = new Mock<ITopicRepository>();
        repository.Setup(x => x.GetWithContentsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        var notifications = new Mock<INotificationService>();
        var handler = new SubjectTopicConvertedHandler(repository.Object, notifications.Object);

        Assert.True((await handler.Handle(request, CancellationToken.None)).IsFailure);
        Assert.Equal(ContentProcessingState.Pending, content.ProcessingState);
        repository.Verify(x => x.TrySaveConversionAsync(It.IsAny<CancellationToken>()), Times.Never);
        notifications.Verify(x => x.SendAsync(It.IsAny<FileUploadedUpdated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotificationFailureAllowsCallbackRetryWithoutRepeatingTransition()
    {
        var (topic, content, request) = CreateAttempt();
        var repository = new Mock<ITopicRepository>();
        repository.Setup(x => x.GetWithContentsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        repository.Setup(x => x.TrySaveConversionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.GetContentCompletionAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(new TopicContentCompletion(3, 5, "Topic", false));
        var notifications = new Mock<INotificationService>();
        notifications.SetupSequence(x => x.SendAsync(It.IsAny<UploadBatchCompleted>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"))
            .Returns(Task.CompletedTask);
        var handler = new SubjectTopicConvertedHandler(repository.Object, notifications.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.Handle(request, CancellationToken.None));
        Assert.Equal(ContentProcessingState.Ready, content.ProcessingState);
        Assert.True((await handler.Handle(request, CancellationToken.None)).IsSuccess);
        repository.Verify(x => x.TrySaveConversionAsync(It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(x => x.SendAsync(It.IsAny<UploadBatchCompleted>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PendingBatchOnlySendsIndividualNotificationAndDeletedContentIsIgnored()
    {
        var (topic, content, request) = CreateAttempt();
        var repository = new Mock<ITopicRepository>();
        repository.Setup(x => x.GetWithContentsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        repository.Setup(x => x.TrySaveConversionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var notifications = new Mock<INotificationService>();
        var handler = new SubjectTopicConvertedHandler(repository.Object, notifications.Object);
        Assert.True((await handler.Handle(request, CancellationToken.None)).IsSuccess);
        notifications.Verify(x => x.SendAsync(It.IsAny<FileUploadedUpdated>(), It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(x => x.SendAsync(It.IsAny<UploadBatchCompleted>(), It.IsAny<CancellationToken>()), Times.Never);
        topic.DeleteContent(content);
        Assert.True((await handler.Handle(request, CancellationToken.None)).IsSuccess);
        notifications.Verify(x => x.SendAsync(It.IsAny<FileUploadedUpdated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidCallbackIsRejectedBeforeLoadingTopic()
    {
        var (_, _, request) = CreateAttempt();
        var repository = new Mock<ITopicRepository>(MockBehavior.Strict);
        var handler = new SubjectTopicConvertedHandler(repository.Object, Mock.Of<INotificationService>());
        Assert.True((await handler.Handle(request with { RequestId = Guid.Empty }, CancellationToken.None)).IsFailure);
        Assert.True((await handler.Handle(request with { EventType = "other" }, CancellationToken.None)).IsFailure);
        Assert.True((await handler.Handle(request with { EventType = "document.conversion.failed" }, CancellationToken.None)).IsFailure);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConcurrencyFailureReloadsBeforeAcceptingConflictingResult()
    {
        var (topic, _, request) = CreateAttempt();
        var (reloaded, content, _) = CreateAttempt();
        typeof(TopicContent).GetProperty(nameof(TopicContent.RequestId))!.SetValue(content, request.RequestId);
        reloaded.FailContentConversion(content, request.RequestId, "first result");
        var repository = new Mock<ITopicRepository>();
        repository.SetupSequence(x => x.GetWithContentsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic).ReturnsAsync(reloaded);
        repository.Setup(x => x.TrySaveConversionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var notifications = new Mock<INotificationService>();
        var handler = new SubjectTopicConvertedHandler(repository.Object, notifications.Object);

        // The other writer accepted a contradictory terminal result.
        Assert.True((await handler.Handle(request, CancellationToken.None)).IsSuccess);
        Assert.Equal(ContentProcessingState.Failed, content.ProcessingState);
        notifications.Verify(x => x.SendAsync(It.IsAny<FileUploadedUpdated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Topic Topic, TopicContent Content, SubjectTopicConvertedCommand Callback) CreateAttempt()
    {
        var topic = Topic.Create(3, "Topic");
        typeof(Topic).GetProperty(nameof(Topic.Id))!.SetValue(topic, 5);
        var content = topic.AddContent("file.pdf", "application/pdf");
        typeof(TopicContent).GetProperty(nameof(TopicContent.Id))!.SetValue(content, 15);
        typeof(TopicContent).GetProperty(nameof(TopicContent.CreatedBy))!.SetValue(content, "12");
        topic.CompleteContentUpload(content, $"https://example.test/topic-content/{Source}", 12);
        return (topic, content, new SubjectTopicConvertedCommand("document.conversion.completed", content.RequestId!.Value,
            new ConversionSource("blob", Source), Destination, "document", new ConversionPayload(15, 5, 12), null));
    }
}
