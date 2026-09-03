using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.UserRoles;

public sealed record RemoveUserRoleCommand([property: JsonIgnore] int UserId, int CenterId, UserRole Role) : ICommand;

internal sealed class RemoveUserRoleCommandHandler(IAdminScopeService adminScope,
                                                   IUnitOfWork unitOfWork,
                                                   IUserRepository userRepository) : ICommandHandler<RemoveUserRoleCommand>
{
    public async ValueTask<Result> Handle(RemoveUserRoleCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Role == UserRole.SuperAdmin)
            return Error.Forbidden("userrole.superadmin_not_allowed", "No se puede eliminar el rol SuperAdmin desde esta operación.");

        var centerCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (centerCheck.IsFailure)
            return centerCheck.Error;

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return Error.NotFound("user.not_found", $"Usuario {request.UserId} no encontrado.");

        user.RemoveRole(request.CenterId, request.Role);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class RemoveUserRoleCommandValidator : AbstractValidator<RemoveUserRoleCommand>
{
    public RemoveUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.CenterId).GreaterThan(0);
        RuleFor(x => x.Role).IsInEnum().Must(r => r != UserRole.SuperAdmin).WithMessage("No se puede eliminar SuperAdmin.");
    }
}
