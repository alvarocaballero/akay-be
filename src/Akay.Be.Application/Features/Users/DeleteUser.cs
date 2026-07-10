using Akay.Be.Application.Abstractions.Identity;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Users;

public sealed record DeleteUserCommand(int Id) : ICommand;

internal sealed class DeleteUserCommandHandler(IAdminScopeService adminScope,
                                               IUnitOfWork unitOfWork,
                                               IUserRepository userRepository,
                                               IIdentityProvisioningService identityProvisioning) : ICommandHandler<DeleteUserCommand>
{
    public async ValueTask<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessUserAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return Error.NotFound("user.not_found", $"Usuario {request.Id} no encontrado.");

        if (user.ExternalId.HasValue)
        {
            try
            {
                await identityProvisioning.DeactivateUserAsync(user.ExternalId.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                return Error.Failure("identity.deactivation_failed", $"No se pudo desactivar el usuario en el proveedor de identidad: {ex.Message}");
            }
        }

        user.Deactivate();
        user.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
