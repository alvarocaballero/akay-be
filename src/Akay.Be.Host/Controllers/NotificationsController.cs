using Akay.Be.Application.Features.Notifications;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591
namespace Akay.Be.Host.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
[Tags("Notifications")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class NotificationsController(IDispatcher dispatcher) : ControllerBase
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
}
#pragma warning restore CS1591
