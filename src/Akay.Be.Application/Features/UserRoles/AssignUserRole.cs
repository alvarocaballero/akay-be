using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.UserRoles;

public sealed record AssignUserRoleCommand(int UserId, int CenterId, UserRole Role) : ICommand<CreatedResponse<int>>;

internal sealed class AssignUserRoleCommandHandler(IAdminScopeService adminScope,
                                                   IUnitOfWork unitOfWork,
                                                   IUserRepository userRepository) : ICommandHandler<AssignUserRoleCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Role == UserRole.SuperAdmin)
            return Error.Forbidden("userrole.superadmin_not_allowed", "No se puede asignar el rol SuperAdmin desde esta operación.");

        var centerCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (centerCheck.IsFailure)
            return centerCheck.Error;

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Error.NotFound("user.not_found", $"Usuario {request.UserId} no encontrado.");

        if (user.RoleAssignments.Any(r => r.CenterId == request.CenterId && r.Role == request.Role))
            return Error.Conflict("userrole.duplicate", "El usuario ya tiene este rol en el centro.");

        user.AssignRole(request.CenterId, request.Role);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var assignment = user.RoleAssignments.First(r => r.CenterId == request.CenterId && r.Role == request.Role);
        return new CreatedResponse<int>(assignment.Id, assignment.CreatedAt);
    }
}

public sealed class AssignUserRoleCommandValidator : AbstractValidator<AssignUserRoleCommand>
{
    public AssignUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.CenterId).GreaterThan(0);
        RuleFor(x => x.Role).IsInEnum().Must(r => r != UserRole.SuperAdmin).WithMessage("No se puede asignar SuperAdmin.");
    }
}
