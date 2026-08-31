using Akay.Be.Application.Features.UserRoles;
using Akay.Be.Application.Features.Users;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591

namespace Akay.Be.Host.Controllers;

/// <summary>
/// Gestión de usuarios del sistema.
/// </summary>
[ApiController]
[Route("api/users")]
[Tags("Users")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class UsersController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Lista los usuarios visibles para el centro indicado en el header X-Center-Id.")]
    [ProducesResponseType<PagedResponse<List<UserListItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IResult> GetAll([FromHeader(Name = "X-Center-Id")] int centerId,
                                      [FromQuery] GetUsersQuery query,
                                      CancellationToken cancellationToken) =>
        (await dispatcher.Send(query with { CenterIds = [centerId] }, cancellationToken)).ToOk();

    [HttpGet("centers")]
    [EndpointSummary("Devuelve los centros sobre los que el usuario actual tiene permisos.")]
    [ProducesResponseType<IReadOnlyList<UserCenterResponse>>(StatusCodes.Status200OK)]
    public async Task<IResult> GetCenters(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetUserCentersQuery(), cancellationToken)).ToOk();

    [HttpGet("profile")]
    [EndpointSummary("Devuelve los roles del usuario actual en el centro indicado junto con su perfil.")]
    [ProducesResponseType<UserProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetProfile([FromHeader(Name = "X-Center-Id")] int centerId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetUserProfileQuery(centerId), cancellationToken)).ToOk();

    [HttpGet("with-roles")]
    [EndpointSummary("Lista los usuarios visibles incluyendo sus roles para el centro indicado en el header X-Center-Id.")]
    [ProducesResponseType<PagedResponse<List<UserWithRolesResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IResult> GetAllWithRoles([FromHeader(Name = "X-Center-Id")] int centerId,
                                               [FromQuery] GetUsersWithRolesQuery query,
                                               CancellationToken cancellationToken) =>
        (await dispatcher.Send(query with { CenterIds = [centerId] }, cancellationToken)).ToOk();

    [HttpGet("{id:int}")]
    [EndpointSummary("Obtiene un usuario por su ID.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetUserByIdQuery(id), cancellationToken)).ToOk();

    [HttpPost]
    [EndpointSummary("Crea un usuario en el proveedor de identidad y en el sistema local.")]
    [EndpointDescription("Le asigna los roles iniciales indicados en centros administrados.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> Create([FromBody] CreateUserCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command, cancellationToken)).ToCreated(value => $"api/users/{value.Id}");

    [HttpPut("{id:int}")]
    [EndpointSummary("Actualiza los datos básicos de un usuario visible.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Update(int id, [FromBody] UpdateUserCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { Id = id }, cancellationToken)).ToNoContent();

    [HttpDelete("{id:int}")]
    [EndpointSummary("Desactiva y elimina un usuario visible.")]
    [EndpointDescription("Soft-delete local; desactivación en Entra en el futuro.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteUserCommand(id), cancellationToken)).ToNoContent();

    // ─── Roles ─────────────────────────────────────────────────────────────────

    [HttpGet("{userId:int}/roles")]
    [EndpointSummary("Lista los roles de un usuario en los centros administrados por el usuario actual.")]
    [ProducesResponseType<IReadOnlyList<UserRoleAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetRoles(int userId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetUserRolesQuery(userId), cancellationToken)).ToOk();

    [HttpPost("{userId:int}/roles")]
    [EndpointSummary("Asigna un rol a un usuario en el centro indicado en el header X-Center-Id.")]
    [ProducesResponseType<CreatedResponse<int>>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> AssignRole(int userId,
                                          [FromHeader(Name = "X-Center-Id")] int centerId,
                                          [FromBody] AssignUserRoleCommand command,
                                          CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { UserId = userId, CenterId = centerId }, cancellationToken)).ToCreated($"api/users/{userId}/roles");

    [HttpDelete("{userId:int}/roles")]
    [EndpointSummary("Elimina un rol de un usuario en un centro administrado.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> RemoveRole(int userId, [FromBody] RemoveUserRoleCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { UserId = userId }, cancellationToken)).ToNoContent();
}

#pragma warning restore CS1591
