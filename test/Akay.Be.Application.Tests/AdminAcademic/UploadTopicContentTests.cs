using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.TopicContents;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.BlobStorage;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Akay.Be.Application.Tests.AdminAcademic;

public class UploadTopicContentTests
{
    [Fact]
    public async Task UploadRejectsUnsupportedExtensionBeforePersistingOrUploading()
    {
        var scope = new Mock<IAdminScopeService>(MockBehavior.Strict);
        var topics = new Mock<ITopicRepository>(MockBehavior.Strict);
        var unitOfWork = new Mock<IUnitOfWork>(MockBehavior.Strict);
        var storage = new Mock<IBlobStorageServiceFactory>(MockBehavior.Strict);
        var notifications = new Mock<INotificationService>(MockBehavior.Strict);
        var user = new Mock<IUserContext>(MockBehavior.Strict);
        var handler = new UploadTopicContentCommandHandler(scope.Object, topics.Object, unitOfWork.Object,
                                                            storage.Object, notifications.Object, user.Object,
                                                            NullLogger<UploadTopicContentCommandHandler>.Instance);
        using var stream = new MemoryStream([1]);

        var result = await handler.Handle(new UploadTopicContentCommand(5, "file.exe", "application/octet-stream", 1, stream),
                                          CancellationToken.None);

        Assert.True(result.IsFailure);
        topics.VerifyNoOtherCalls();
        unitOfWork.VerifyNoOtherCalls();
        storage.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadStopsBeforeStorageWhenCreatorWasNotAudited()
    {
        var topic = Topic.Create(10, "Topic");
        var topics = new Mock<ITopicRepository>();
        topics.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        topics.Setup(x => x.AddContent(topic, "file.pdf", "application/pdf", null, null))
              .Returns(() => topic.AddContent("file.pdf", "application/pdf"));
        var scope = new Mock<IAdminScopeService>();
        scope.Setup(x => x.EnsureCanWriteSubjectContentAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.ResultSaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result<int>.Success(1));
        var storage = new Mock<IBlobStorageServiceFactory>(MockBehavior.Strict);
        var user = new Mock<IUserContext>();
        user.SetupGet(x => x.UserId).Returns(12);
        var handler = new UploadTopicContentCommandHandler(scope.Object, topics.Object, unitOfWork.Object,
                                                           storage.Object, Mock.Of<INotificationService>(), user.Object,
                                                           NullLogger<UploadTopicContentCommandHandler>.Instance);
        using var stream = new MemoryStream([1]);

        var result = await handler.Handle(new UploadTopicContentCommand(5, "file.pdf", "application/pdf", 1, stream),
                                          CancellationToken.None);

        Assert.True(result.IsFailure);
        storage.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadPersistsAndNotifiesTheFinalState(bool uploadFails)
    {
        var topic = Topic.Create(10, "Topic");
        typeof(Topic).GetProperty(nameof(Topic.Id))!.SetValue(topic, 5);
        var topics = new Mock<ITopicRepository>();
        topics.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        var scope = new Mock<IAdminScopeService>();
        scope.Setup(x => x.EnsureCanWriteSubjectContentAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        TopicContent? created = null;
        topics.Setup(x => x.AddContent(topic, "file.pdf", "application/pdf", null, null))
              .Returns(() => created = topic.AddContent("file.pdf", "application/pdf"));
        var savedStates = new List<ContentProcessingState>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.ResultSaveChangesAsync(It.IsAny<CancellationToken>()))
           .ReturnsAsync(() =>
           {
               savedStates.Add(created!.ProcessingState);
               typeof(TopicContent).GetProperty(nameof(TopicContent.CreatedBy))!.SetValue(created, "12");
               return Result<int>.Success(1);
           });
        var blob = new Mock<IBlobStorageService>();
        var upload = blob.Setup(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), false, false, It.IsAny<CancellationToken>()));
        if (uploadFails)
            upload.ThrowsAsync(new IOException("storage unavailable"));
        else
            upload.ReturnsAsync("https://example.invalid/blob");
        var factory = new Mock<IBlobStorageServiceFactory>();
        factory.Setup(x => x.CreateAsync("topic-content", false, null, false, It.IsAny<CancellationToken>())).ReturnsAsync(blob.Object);
        var notifications = new Mock<INotificationService>();
        var user = new Mock<IUserContext>();
        user.SetupGet(x => x.UserId).Returns(12);
        var handler = new UploadTopicContentCommandHandler(scope.Object, topics.Object, uow.Object,
                                                           factory.Object, notifications.Object, user.Object,
                                                           NullLogger<UploadTopicContentCommandHandler>.Instance);
        using var stream = new MemoryStream([1, 2, 3]);

        var result = await handler.Handle(new UploadTopicContentCommand(5, "file.pdf", "application/pdf", 3, stream), CancellationToken.None);

        Assert.Equal(!uploadFails, result.IsSuccess);
        topics.Verify(x => x.AddContent(topic, "file.pdf", "application/pdf", null, null), Times.Once);
        Assert.Equal([ContentProcessingState.Uploading, uploadFails ? ContentProcessingState.UploadFailed : ContentProcessingState.Pending], savedStates);
        Assert.Equal(uploadFails ? null : "https://example.invalid/blob", created!.SourceContentUrl);
        Assert.Equal("file.pdf", created.FileName);
        Assert.Equal(!uploadFails, created.RequestId.HasValue);
        blob.Verify(x => x.UploadAsync(It.Is<string>(name => IsExpectedBlobName(name)), It.IsAny<Stream>(), "application/pdf", false, false, It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(x => x.SendAsync(It.IsAny<TopicContentStateChanged>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static bool IsExpectedBlobName(string name)
    {
        const string prefix = "subjects/10/topics/5/sources/";
        return name.StartsWith(prefix, StringComparison.Ordinal)
               && name.EndsWith(".pdf", StringComparison.Ordinal)
               && Guid.TryParse(name[prefix.Length..^4], out _);
    }
}
