using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Users;

public sealed record UpdateUserCommand([property: JsonIgnore] int Id,
                                       string Email,
                                       string FirstName,
                                       string LastName,
                                       bool IsActive) : ICommand<UserResponse>;

internal sealed class UpdateUserCommandHandler(IAdminScopeService adminScope,
                                                 IUnitOfWork unitOfWork,
                                                 IUserRepository userRepository) : ICommandHandler<UpdateUserCommand, UserResponse>
{
    public async ValueTask<Result<UserResponse>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessUserAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null || user.DeletedAt is not null)
            return UserErrors.NotFound(request.Id);

        if (!string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase) &&
            await userRepository.EmailExistsAsync(request.Email, cancellationToken))
        {
            return UserErrors.EmailExists();
        }

        user.UpdateProfile(request.Email, request.FirstName, request.LastName);

        if (request.IsActive && !user.IsActive)
            user.Activate();
        else if (!request.IsActive && user.IsActive)
            user.Deactivate();

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserResponse(user.Id, user.ExternalId, user.Email, user.FirstName, user.LastName, user.IsActive);
    }
}

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}
