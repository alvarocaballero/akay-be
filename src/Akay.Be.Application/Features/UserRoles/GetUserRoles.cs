using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.UserRoles;

public sealed record GetUserRolesQuery(int UserId) : IQuery<IReadOnlyList<UserRoleAssignmentResponse>>;

internal sealed class GetUserRolesQueryHandler(IAdminScopeService adminScope,
                                               IUserRepository userRepository) : IQueryHandler<GetUserRolesQuery, IReadOnlyList<UserRoleAssignmentResponse>>
{
    public async ValueTask<Result<IReadOnlyList<UserRoleAssignmentResponse>>> Handle(GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCenters = await adminScope.GetAdminCenterIdsAsync(cancellationToken);
        if (adminCenters.Count == 0)
            return new List<UserRoleAssignmentResponse>();

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Error.NotFound("user.not_found", $"Usuario {request.UserId} no encontrado.");

        return user.RoleAssignments
            .Where(r => r.CenterId.HasValue && adminCenters.Contains(r.CenterId.Value))
            .Select(r => new UserRoleAssignmentResponse(r.UserId, r.CenterId, r.Role))
            .ToList();
    }
}
