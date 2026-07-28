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
                                                IUserRepository userRepository) : ICommandHandler<CreateUserCommand, CreatedResponse<int>>
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
            return UserErrors.EmailExists();

        var user = User.Create(request.Email, request.FirstName, request.LastName);

        foreach (var role in request.InitialRoles)
        {
            if (role.Role == UserRole.SuperAdmin)
                return UserErrors.SuperAdminNotAllowed();

            user.AssignRole(role.CenterId, role.Role);
        }

        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(user.Id, user.CreatedAt);
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
