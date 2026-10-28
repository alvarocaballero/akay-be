namespace Akay.Be.Application.Abstractions.Services;

public interface INotification
{
    int UserId { get; }
    string Type { get; }
}

public interface IPersistentNotification : INotification
{
    string Title { get; }
    string Message { get; }
    string? Url { get; }
}

public interface INotificationService
{
    Task SendAsync<TNotification>(TNotification notification,
                                 CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
