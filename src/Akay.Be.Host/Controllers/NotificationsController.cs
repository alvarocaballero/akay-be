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
    [EndpointSummary("Lists the current user's notifications, newest first.")]
    [ProducesResponseType<PagedResponse<List<NotificationResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IResult> Get([FromQuery] GetNotificationsQuery query, CancellationToken cancellationToken) =>
        (await dispatcher.Send(query, cancellationToken)).ToOk();

    [HttpGet("unread-count")]
    [EndpointSummary("Counts the current user's unread notifications.")]
    [ProducesResponseType<int>(StatusCodes.Status200OK)]
    public async Task<IResult> GetUnreadCount(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetUnreadCountQuery(), cancellationToken)).ToOk();

    [HttpPatch("{id:int}/read")]
    [EndpointSummary("Marks an owned notification as read without changing its first read timestamp.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> MarkRead(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new MarkNotificationReadCommand(id), cancellationToken)).ToNoContent();

    [HttpPatch("read-all")]
    [EndpointSummary("Marks all of the current user's unread notifications as read.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IResult> MarkAllRead(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new MarkAllNotificationsReadCommand(), cancellationToken)).ToNoContent();

    [HttpDelete("{id:int}")]
    [EndpointSummary("Permanently deletes an owned notification.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteNotificationCommand(id), cancellationToken)).ToNoContent();
}
#pragma warning restore CS1591
