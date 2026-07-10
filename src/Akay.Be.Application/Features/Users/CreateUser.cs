using Akay.Be.Application.Abstractions.Identity;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Users;

public sealed record CreateUserInitialRole(int CenterId, UserRole Role);

public sealed record CreateUserCommand(string Email,
                                       string FirstName,
                                       string LastName,
                                       IReadOnlyList<CreateUserInitialRole> InitialRoles) : ICommand<CreatedResponse<int>>;

internal sealed class CreateUserCommandHandler(IAdminScopeService adminScope,
                                               IUnitOfWork unitOfWork,
                                               IUserRepository userRepository,
                                               IIdentityProvisioningService identityProvisioning) : ICommandHandler<CreateUserCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var requestedCenters = request.InitialRoles.Select(r => r.CenterId).ToHashSet();
        if (requestedCenters.Count > 0)
        {
            var centerCheck = await adminScope.EnsureAdminOfAllCentersAsync(requestedCenters, cancellationToken);
            if (centerCheck.IsFailure)
                return centerCheck.Error;
        }

        if (await userRepository.EmailExistsAsync(request.Email, cancellationToken))
            return Error.Conflict("user.email_exists", "Ya existe un usuario con ese email.");

        var temporaryPassword = GenerateTemporaryPassword();

        Guid externalId;
        try
        {
            externalId = await identityProvisioning.CreateUserAsync(request.Email,
                                                                    request.FirstName,
                                                                    request.LastName,
                                                                    temporaryPassword,
                                                                    cancellationToken);
        }
        catch (Exception ex)
        {
            return Error.Failure("identity.provisioning_failed", $"No se pudo crear el usuario en el proveedor de identidad: {ex.Message}");
        }

        var user = User.Create(request.Email, request.FirstName, request.LastName);
        user.SetExternalId(externalId);

        foreach (var role in request.InitialRoles)
        {
            if (role.Role == UserRole.SuperAdmin)
                return Error.Forbidden("user.superadmin_not_allowed", "No se puede asignar el rol SuperAdmin al crear un usuario.");

            user.AssignRole(role.CenterId, role.Role);
        }

        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(user.Id, user.CreatedAt);
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789!@#$%^&*";
        var buffer = new byte[16];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(buffer);
        return new string(buffer.Select(b => chars[b % chars.Length]).ToArray());
    }
}

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.InitialRoles).NotNull();
        RuleForEach(x => x.InitialRoles).ChildRules(role =>
        {
            role.RuleFor(x => x.CenterId).GreaterThan(0);
            role.RuleFor(x => x.Role).IsInEnum();
        });
    }
}
