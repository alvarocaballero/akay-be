using Akay.Be.Application.Notifications;
using Akay.Be.Infrastructure.SignalRHubs;
using Akay.To.Azure.Infrastructure.SignalR;

namespace Akay.Be.Infrastructure.Services;

public sealed class SignalRNotificationPublisher(ISignalRHubService<NotificationHub, NotificationMessage> hubService)
    : INotificationPublisher
{
    public Task PublishAsync(string userId, NotificationMessage message, CancellationToken cancellationToken) =>
        hubService.SendUserAsync(userId, message, cancellationToken: cancellationToken);
}
