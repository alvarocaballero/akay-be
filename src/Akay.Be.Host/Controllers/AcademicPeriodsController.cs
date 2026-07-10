using Akay.Be.Application.Features.AcademicPeriods;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591

namespace Akay.Be.Host.Controllers;

/// <summary>
/// Gestión de periodos académicos.
/// </summary>
[ApiController]
[Route("api/academic-periods")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class AcademicPeriodsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Lista los periodos académicos de los centros administrados por el usuario actual.")]
    [ProducesResponseType<IReadOnlyList<AcademicPeriodResponse>>(StatusCodes.Status200OK)]
    public async Task<IResult> GetAll(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetAcademicPeriodsQuery(), cancellationToken)).ToOk();

    [HttpGet("{id:int}")]
    [EndpointSummary("Obtiene un periodo académico por su ID.")]
    [ProducesResponseType<AcademicPeriodResponse>(StatusCodes.Status200OK)]//&
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetAcademicPeriodByIdQuery(id), cancellationToken)).ToOk();

    [HttpPost]
    [EndpointSummary("Crea un periodo académico en un centro administrado.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create([FromBody] CreateAcademicPeriodCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command, cancellationToken)).ToCreated(value => $"api/academic-periods/{value.Id}");

    [HttpPut("{id:int}")]
    [EndpointSummary("Actualiza un periodo académico visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(int id, [FromBody] UpdateAcademicPeriodCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { Id = id }, cancellationToken)).ToNoContent();

    [HttpDelete("{id:int}")]
    [EndpointSummary("Elimina un periodo académico visible (soft-delete).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteAcademicPeriodCommand(id), cancellationToken)).ToNoContent();
}

#pragma warning restore CS1591
