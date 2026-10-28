using System.Text.Json;
using System.Text.Json.Serialization;

namespace Akay.Be.Application.Notifications;

public sealed record NotificationMessage(string Type,
                                         JsonElement Payload,
                                         [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? NotificationId = null,
                                         [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title = null,
                                         [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Message = null,
                                         [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url = null);

/// <summary>Application port; the adapter delegates transport to Akay.To.Azure.</summary>
public interface INotificationPublisher
{
    Task PublishAsync(string userId, NotificationMessage message, CancellationToken cancellationToken);
}
