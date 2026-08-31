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
[Tags("AcademicPeriods")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class AcademicPeriodsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "admin,teacher")]
    [EndpointSummary("Lista los periodos académicos del centro indicado en el header X-Center-Id.")]
    [ProducesResponseType<IReadOnlyList<AcademicPeriodResponse>>(StatusCodes.Status200OK)]
    public async Task<IResult> GetAll([FromHeader(Name = "X-Center-Id")] int centerId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetAcademicPeriodsQuery(centerId), cancellationToken)).ToOk();

    [HttpGet("{id:int}")]
    [Authorize(Roles = "admin,teacher")]
    [EndpointSummary("Obtiene un periodo académico por su ID.")]
    [ProducesResponseType<AcademicPeriodResponse>(StatusCodes.Status200OK)]//&
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetAcademicPeriodByIdQuery(id), cancellationToken)).ToOk();

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EndpointSummary("Crea un periodo académico en el centro indicado en el header X-Center-Id.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create([FromHeader(Name = "X-Center-Id")] int centerId,
                                      [FromBody] CreateAcademicPeriodCommand command,
                                      CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { CenterId = centerId }, cancellationToken)).ToCreated(value => $"api/academic-periods/{value.Id}");

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    [EndpointSummary("Actualiza un periodo académico visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(int id, [FromBody] UpdateAcademicPeriodCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { Id = id }, cancellationToken)).ToNoContent();

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    [EndpointSummary("Elimina un periodo académico visible (soft-delete).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteAcademicPeriodCommand(id), cancellationToken)).ToNoContent();
}

#pragma warning restore CS1591
