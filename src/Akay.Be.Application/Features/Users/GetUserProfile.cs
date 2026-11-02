using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Users;

public sealed record GetUserProfileQuery(int CenterId) : IQuery<UserProfileResponse>;

public sealed record UserProfileResponse(int Id,
                                         Guid? ExternalId,
                                         string Email,
                                         string FirstName,
                                         string LastName,
                                         bool IsActive,
                                         IReadOnlyList<UserRole> Roles,
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

        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null)
            return Error.NotFound("user.not_found", "Usuario no encontrado.");

        var roles = user.RoleAssignments
            .Where(r => r.CenterId == request.CenterId)
            .Select(r => r.Role)
            .ToList();

        var profile = user.Profile;

        return new UserProfileResponse(user.Id,
                                       user.ExternalId,
                                       user.Email,
                                       user.FirstName,
                                       user.LastName,
                                       user.IsActive,
                                       roles,
                                       profile?.Language ?? "es-ES",
                                       profile?.DarkMode ?? false);
    }
}
