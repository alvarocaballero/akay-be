using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Users;

public sealed record GetUserProfileQuery(int CenterId) : IQuery<UserProfileResponse>;

public sealed record UserProfileResponse(IReadOnlyList<UserRole> Roles,
                                         string Language,
                                         bool DarkMode);

internal sealed class GetUserProfileQueryHandler(IUserContext userContext,
                                                 IUserRepository userRepository) : IQueryHandler<GetUserProfileQuery, UserProfileResponse>
{
    public async ValueTask<Result<UserProfileResponse>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userId = userContext.UserId;
        if (userId <= 0)
            return Error.Unauthorized("user.unauthenticated", "Usuario no autenticado.");

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return Error.NotFound("user.not_found", "Usuario no encontrado.");

        var roles = user.RoleAssignments
            .Where(r => r.CenterId == request.CenterId && r.DeletedAt == null)
            .Select(r => r.Role)
            .ToList();

        var profile = user.Profile;

        return new UserProfileResponse(roles,
                                       profile?.Language ?? "es-ES",
                                       profile?.DarkMode ?? false);
    }
}
