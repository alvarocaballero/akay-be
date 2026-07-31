using Akay.Be.Application.Features.Students;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591

namespace Akay.Be.Host.Controllers;

/// <summary>
/// Gestión de perfiles de estudiantes.
/// </summary>
[ApiController]
[Route("api/students")]
[Tags("Students")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class StudentsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Lista paginada los estudiantes del centro indicado en el header X-Center-Id.")]
    [ProducesResponseType<PagedResponse<List<StudentResponse>>>(StatusCodes.Status200OK)]
    public async Task<IResult> GetAll([FromHeader(Name = "X-Center-Id")] int centerId,
                                      [FromQuery] GetStudentsRequest request,
                                      CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetStudentsQuery(centerId, request.Search, request.IsActive), cancellationToken)).ToOk();

    [HttpGet("{id:int}")]
    [EndpointSummary("Obtiene un estudiante por su ID.")]
    [ProducesResponseType<StudentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetStudentByIdQuery(id), cancellationToken)).ToOk();

    [HttpPost]
    [EndpointSummary("Crea un perfil de estudiante en el centro indicado en el header X-Center-Id y le asigna el rol Student.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create([FromHeader(Name = "X-Center-Id")] int centerId,
                                      [FromBody] CreateStudentCommand command,
                                      CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { CenterId = centerId }, cancellationToken)).ToCreated(value => $"api/students/{value.Id}");

    [HttpGet("{id:int}/details")]
    [EndpointSummary("Obtiene los datos de un estudiante con los cursos en los que está matriculado y sus asignaturas, filtrados por el centro del header X-Center-Id.")]
    [ProducesResponseType<StudentDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetDetails(int id,
                                          [FromHeader(Name = "X-Center-Id")] int centerId,
                                          CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetStudentDetailsQuery(id, centerId), cancellationToken)).ToOk();

    [HttpPut("{id:int}")]
    [EndpointSummary("Actualiza un estudiante visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(int id, [FromBody] UpdateStudentCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { Id = id }, cancellationToken)).ToNoContent();

    [HttpDelete("{id:int}")]
    [EndpointSummary("Elimina un estudiante visible (soft-delete).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteStudentCommand(id), cancellationToken)).ToNoContent();
}

#pragma warning restore CS1591
