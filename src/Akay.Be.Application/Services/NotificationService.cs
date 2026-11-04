using System.Globalization;
using System.Text.Json;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Notifications;
using Akay.Be.Domain.Entities.Notification;
using Microsoft.Extensions.Logging;

namespace Akay.Be.Application.Services;

public sealed class NotificationService(INotificationRepository repository,
                                        INotificationPublisher publisher,
                                        ILogger<NotificationService> logger) : INotificationService
{
    public async Task SendAsync<TNotification>(TNotification notification,
                                               CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(notification.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.Type);
        cancellationToken.ThrowIfCancellationRequested();

        // Serialize the runtime type, including additional fields when called through INotification.
        var payload = JsonSerializer.SerializeToElement(notification, notification.GetType(), JsonSerializerOptions.Web);

        PersistentNotification? stored = null;

        if (notification is IPersistentNotification persistent)
        {
            stored = PersistentNotification.Create(notification.UserId,
                                                   notification.Type,
                                                   persistent.Title,
                                                   persistent.Message,
                                                   persistent.Url,
                                                   payload.GetRawText(),
                                                   DateTimeOffset.UtcNow);

            await repository.PersistAsync(stored, cancellationToken);
        }

        var message = new NotificationMessage(notification.Type, payload, stored?.Id,
                                               stored?.Title, stored?.Message, stored?.Url);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // SignalR targeting takes the local user id as an invariant string; storage keeps the int identity.
            var signalRUserId = notification.UserId.ToString(CultureInfo.InvariantCulture);
            await publisher.PublishAsync(signalRUserId, message, cancellationToken);
        }
        catch (Exception exception)
        {
            // Persistence has already committed. Publication is best effort, never a business rollback.
            logger.LogWarning(exception, "Notification publication failed for type {NotificationType}, id {NotificationId}",
                              notification.Type, stored?.Id);
        }
    }
}
