using System.Text.Json;
using Akay.Be.Domain.Entities.Notification;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Notifications;

public sealed record NotificationResponse(int Id,
                                          string Type,
                                          string Title,
                                          string Message,
                                          string? Url,
                                          JsonElement? Payload,
                                          DateTimeOffset CreatedAt,
                                          DateTimeOffset? ReadAt)
{
    internal static NotificationResponse From(PersistentNotification notification) =>
        new(notification.Id, notification.Type, notification.Title, notification.Message, notification.Url,
            notification.Payload is null ? null : JsonSerializer.Deserialize<JsonElement>(notification.Payload),
            notification.CreatedAt, notification.ReadAt);
}

internal static class NotificationAccess
{
    internal static readonly Error NotFound = Error.NotFound("notification.not_found", "Notification not found.");

    internal static Result<int> GetUserId(IUserContext user) =>
        user.IsAuthenticated && user.UserId > 0
            ? user.UserId
            : Error.Unauthorized("notification.unauthenticated", "An authenticated application user is required.");
}
