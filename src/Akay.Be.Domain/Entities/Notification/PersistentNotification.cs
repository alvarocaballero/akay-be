using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Notification;

public sealed class PersistentNotification : Entity<int>
{
    private PersistentNotification() { }

    public int UserId { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public string? Url { get; private set; }

    public string? Payload { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public static PersistentNotification Create(int userId,
                                                string type,
                                                string title,
                                                string message,
                                                string? url,
                                                string? payload,
                                                DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (url is not null && (url.StartsWith("//", StringComparison.Ordinal)
                                || url.Contains('\\') || Uri.TryCreate(url, UriKind.Absolute, out _)))
            throw new ArgumentException("Notification URL must be an internal application path.", nameof(url));

        return new PersistentNotification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Url = url,
            Payload = payload,
            CreatedAt = createdAt.ToUniversalTime()
        };
    }

    public void MarkRead(DateTimeOffset readAt) => ReadAt ??= readAt.ToUniversalTime();
}
