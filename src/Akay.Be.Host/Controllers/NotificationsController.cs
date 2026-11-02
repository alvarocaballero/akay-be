using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.Notifications;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591
namespace Akay.Be.Host.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
[Tags("Notifications")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class NotificationsController(IDispatcher dispatcher, INotificationService notifications) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Lista las notificaciones del usuario actual, de la más reciente a la más antigua.")]
    [ProducesResponseType<PagedResponse<List<NotificationResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Get([FromQuery] GetNotificationsQuery query, CancellationToken cancellationToken) =>
        (await dispatcher.Send(query, cancellationToken)).ToOk();

    [HttpGet("unread-count")]
    [EndpointSummary("Cuenta las notificaciones no leídas del usuario actual.")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<IResult> GetUnreadCount(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetUnreadCountQuery(), cancellationToken)).ToOk();

    [HttpPatch("{id:int}/read")]
    [EndpointSummary("Marca como leída una notificación del usuario sin modificar su fecha de primera lectura.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> MarkRead(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new MarkNotificationReadCommand(id), cancellationToken)).ToNoContent();

    [HttpPatch("read-all")]
    [EndpointSummary("Marca como leídas todas las notificaciones no leídas del usuario actual.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> MarkAllRead(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new MarkAllNotificationsReadCommand(), cancellationToken)).ToNoContent();

    [HttpDelete("{id:int}")]
    [EndpointSummary("Elimina definitivamente una notificación del usuario actual.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteNotificationCommand(id), cancellationToken)).ToNoContent();

    [HttpPost("test")]
    [AllowAnonymous]
    [EndpointSummary("TEMPORAL: envía una notificación de prueba al usuario 1 sin autorización.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> SendTest(CancellationToken cancellationToken)
    {
        await notifications.SendAsync(new TestNotification(1,
                                                           "test.signalr",
                                                           "Notificación de prueba",
                                                           "Conexión SignalR verificada correctamente.",
                                                           "/notifications",
                                                           42,
                                                           "test-endpoint",
                                                           DateTimeOffset.UtcNow), cancellationToken);
        return Results.NoContent();
    }

    private sealed record TestNotification(int UserId,
                                           string Type,
                                           string Title,
                                           string Message,
                                           string? Url,
                                           int TopicId,
                                           string Source,
                                           DateTimeOffset SentAt) : IPersistentNotification;
}
#pragma warning restore CS1591
