using Akay.Be.Application.Features.Topics;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591

namespace Akay.Be.Host.Controllers;

/// <summary>
/// Gestión de temas de una asignatura.
/// </summary>
[ApiController]
[Route("api")]
[Tags("Topics")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class TopicsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet("subjects/{subjectId:int}/topics")]
    [EndpointSummary("Lista los temas de una asignatura ordenados por displayOrder.")]
    [ProducesResponseType<IReadOnlyList<TopicResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetBySubject(int subjectId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetTopicsBySubjectQuery(subjectId), cancellationToken)).ToOk();

    [HttpPost("subjects/{subjectId:int}/topics")]
    [EndpointSummary("Crea un tema en la posición indicada o al final de la asignatura.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Create(int subjectId, [FromBody] CreateTopicCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { SubjectId = subjectId }, cancellationToken))
            .ToCreated(value => $"api/subjects/{subjectId}/topics/{value.Id}");

    [HttpPut("topics/{id:int}")]
    [EndpointSummary("Actualiza los datos de un tema y, opcionalmente, su posición.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(int id, [FromBody] UpdateTopicCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { Id = id }, cancellationToken)).ToNoContent();

    [HttpDelete("topics/{id:int}")]
    [EndpointSummary("Elimina un tema (soft-delete) y compacta el orden de los restantes.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteTopicCommand(id), cancellationToken)).ToNoContent();

    [HttpPatch("topics/{id:int}/display-order")]
    [EndpointSummary("Reordena un tema desplazando los siguientes.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Reorder(int id, [FromBody] ReorderTopicCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { Id = id }, cancellationToken)).ToNoContent();
}

#pragma warning restore CS1591
