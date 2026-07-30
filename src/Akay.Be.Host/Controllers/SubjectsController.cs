using Akay.Be.Application.Features.Subjects;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591

namespace Akay.Be.Host.Controllers;

/// <summary>
/// Gestión de asignaturas y sus administradores.
/// </summary>
[ApiController]
[Route("api/subjects")]
[Tags("Subjects")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class SubjectsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Lista las asignaturas visibles para los centros administrados por el usuario actual.")]
    [ProducesResponseType<IReadOnlyList<SubjectResponse>>(StatusCodes.Status200OK)]
    public async Task<IResult> GetAll(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetSubjectsQuery(), cancellationToken)).ToOk();

    [HttpGet("center")]
    [EndpointSummary("Lista las asignaturas del centro indicado en el header X-Center-Id.")]
    [ProducesResponseType<IReadOnlyList<SubjectResponse>>(StatusCodes.Status200OK)]
    public async Task<IResult> GetByCenter([FromHeader(Name = "X-Center-Id")] int centerId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetSubjectsQuery { CenterId = centerId }, cancellationToken)).ToOk();

    [HttpGet("{id:int}")]
    [EndpointSummary("Obtiene una asignatura por su ID.")]
    [ProducesResponseType<SubjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetSubjectByIdQuery(id), cancellationToken)).ToOk();

    [HttpPost]
    [EndpointSummary("Crea una asignatura asociada a uno o varios centros administrados.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create([FromBody] CreateSubjectCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command, cancellationToken)).ToCreated(value => $"api/subjects/{value.Id}");

    [HttpPut("{id:int}")]
    [EndpointSummary("Actualiza el nombre y la descripción de una asignatura visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(int id, [FromBody] UpdateSubjectCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { Id = id }, cancellationToken)).ToNoContent();

    [HttpDelete("{id:int}")]
    [EndpointSummary("Elimina una asignatura visible (soft-delete).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteSubjectCommand(id), cancellationToken)).ToNoContent();

    [HttpPost("{subjectId:int}/centers")]
    [EndpointSummary("Añade un centro administrado a una asignatura visible.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> AddCenter(int subjectId, [FromBody] AddSubjectCenterCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { SubjectId = subjectId }, cancellationToken)).ToCreated($"api/subjects/{subjectId}/centers");

    [HttpDelete("{subjectId:int}/centers/{centerId:int}")]
    [EndpointSummary("Elimina un centro asignado de una asignatura visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> RemoveCenter(int subjectId, int centerId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new RemoveSubjectCenterCommand(subjectId, centerId), cancellationToken)).ToNoContent();

    [HttpPost("{subjectId:int}/admins")]
    [EndpointSummary("Asigna un administrador de asignatura.")]
    [EndpointDescription("Debe tener rol Teacher o Admin en un centro visible.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> AddAdmin(int subjectId, [FromBody] AddSubjectAdminCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { SubjectId = subjectId }, cancellationToken)).ToCreated($"api/subjects/{subjectId}/admins");

    [HttpDelete("{subjectId:int}/admins/{userId:int}")]
    [EndpointSummary("Elimina un administrador de una asignatura visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> RemoveAdmin(int subjectId, int userId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new RemoveSubjectAdminCommand(subjectId, userId), cancellationToken)).ToNoContent();
}

#pragma warning restore CS1591
