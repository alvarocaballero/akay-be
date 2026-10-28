using Akay.Be.Domain.Entities.Notification;

namespace Akay.Be.Domain.Tests.Notifications;

public sealed class NotificationTests
{
    [Fact]
    public void MarkRead_PreservesFirstTimestamp_AndNormalizesUtc()
    {
        var time = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.FromHours(2));
        var notification = PersistentNotification.Create(12, "done", "Title", "Message", null, null, time);
        notification.MarkRead(time.AddMinutes(1));
        notification.MarkRead(time.AddMinutes(2));

        Assert.Equal(0, notification.Id);
        Assert.Equal(TimeSpan.Zero, notification.CreatedAt.Offset);
        Assert.Equal(time.AddMinutes(1), notification.ReadAt);
        Assert.Null(notification.Url);
        Assert.Null(notification.Payload);
    }

    [Theory]
    [InlineData("https://example.test")]
    [InlineData("//example.test")]
    [InlineData("\\example.test")]
    public void Url_MustBeAnInternalPath(string url) =>
        Assert.Throws<ArgumentException>(() => PersistentNotification.Create(12, "done", "Title", "Message", url, null, DateTimeOffset.UtcNow));
}
