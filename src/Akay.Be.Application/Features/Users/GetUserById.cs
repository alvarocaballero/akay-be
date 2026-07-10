using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Entities.Identity;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Users;

public sealed record GetUserByIdQuery(int Id) : IQuery<UserResponse>;

internal sealed class GetUserByIdQueryHandler(IAdminScopeService adminScope,
                                              IUserRepository userRepository) : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async ValueTask<Result<UserResponse>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessUserAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return Error.NotFound("user.not_found", $"Usuario {request.Id} no encontrado.");

        return Map(user);
    }

    private static UserResponse Map(User user) =>
        new(user.Id, user.ExternalId, user.Email, user.FirstName, user.LastName, user.IsActive);
}
