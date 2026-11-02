using System.Text.Json;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Notifications;
using Akay.Be.Application.Services;
using Akay.Be.Domain.Entities.Notification;
using Microsoft.Extensions.Logging;
using Moq;

namespace Akay.Be.Application.Tests.Notifications;

public sealed class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<INotificationPublisher> publisher = new(MockBehavior.Strict);
    private readonly Mock<ILogger<NotificationService>> logger = new();

    private NotificationService CreateService() => new(repository.Object, publisher.Object, logger.Object);

    [Fact]
    public async Task Ephemeral_PublishesWithoutPersistence()
    {
        var ct = TestContext.Current.CancellationToken;
        publisher.Setup(p => p.PublishAsync("12", It.IsAny<NotificationMessage>(), ct))
                 .Callback<string, NotificationMessage, CancellationToken>((user, message, token) =>
                 {
                     Assert.Equal("progress", message.Type);
                     Assert.Equal(70, message.Payload.GetProperty("percent").GetInt32());
                     Assert.Null(message.NotificationId);
                     Assert.False(message.IsPersistent);
                     Assert.Null(message.Title);
                     Assert.DoesNotContain("notificationId", JsonSerializer.Serialize(message, JsonSerializerOptions.Web));
                     Assert.Contains("\"isPersistent\":false", JsonSerializer.Serialize(message, JsonSerializerOptions.Web));
                 }).Returns(Task.CompletedTask);

        await CreateService().SendAsync(new Progress(12, "progress", 70), ct);

        repository.VerifyNoOtherCalls();
        publisher.VerifyAll();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/topics/42")]
    public async Task Persistent_CommitsBeforePublishing_AndPreservesRuntimePayload(string? url)
    {
        var ct = TestContext.Current.CancellationToken;
        PersistentNotification? persisted = null;
        repository.Setup(r => r.PersistAsync(It.IsAny<PersistentNotification>(), ct))
                  .Callback<PersistentNotification, CancellationToken>((record, token) => persisted = record)
                  .Returns(Task.CompletedTask);
        publisher.Setup(p => p.PublishAsync("12", It.IsAny<NotificationMessage>(), ct))
                 .Callback<string, NotificationMessage, CancellationToken>((user, message, token) =>
                 {
                     Assert.NotNull(persisted);
                     Assert.Equal(persisted.Id, message.NotificationId);
                     Assert.True(message.IsPersistent);
                     Assert.Equal("Done", message.Title);
                     Assert.Equal("Finished", message.Message);
                     Assert.Equal(url, message.Url);
                     Assert.Equal(42, message.Payload.GetProperty("topicId").GetInt32());
                     using var stored = JsonDocument.Parse(persisted.Payload!);
                     Assert.Equal(42, stored.RootElement.GetProperty("topicId").GetInt32());
                 }).Returns(Task.CompletedTask);
        INotification notification = new Completed(12, "completed", "Done", "Finished", url, 42);

        await CreateService().SendAsync(notification, ct);

        repository.VerifyAll();
        publisher.VerifyAll();
    }

    [Fact]
    public async Task PersistenceFailure_PreventsPublication()
    {
        var ct = TestContext.Current.CancellationToken;
        repository.Setup(r => r.PersistAsync(It.IsAny<PersistentNotification>(), ct))
                  .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var notification = new Completed(12, "completed", "Done", "Finished", null, 42);
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().SendAsync(notification, ct));

        publisher.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationFailure_IsLoggedWithoutRePersisting(bool persistent)
    {
        var ct = TestContext.Current.CancellationToken;
        repository.Setup(r => r.PersistAsync(It.IsAny<PersistentNotification>(), ct)).Returns(Task.CompletedTask);
        publisher.Setup(p => p.PublishAsync("12", It.IsAny<NotificationMessage>(), ct))
                 .ThrowsAsync(new InvalidOperationException("SignalR unavailable"));
        INotification notification = persistent
            ? new Completed(12, "completed", "Done", "Finished", null, 42)
            : new Progress(12, "progress", 70);

        await CreateService().SendAsync(notification, ct);

        repository.Verify(r => r.PersistAsync(It.IsAny<PersistentNotification>(), ct), persistent ? Times.Once() : Times.Never());
        logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(),
                                 It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
                                 It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once());
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AlreadyCancelled_DoesNotPersistOrPublish()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var notification = new Progress(12, "progress", 70);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().SendAsync(notification, cancellation.Token));

        repository.VerifyNoOtherCalls();
        publisher.VerifyNoOtherCalls();
    }

    private sealed record Progress(int UserId, string Type, int Percent) : INotification;
    private sealed record Completed(int UserId, string Type, string Title, string Message, string? Url, int TopicId)
        : IPersistentNotification;
}
