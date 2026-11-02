using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Akay.Be.Application.Features.Users;

public sealed record UpdateUserProfileCommand(string Language,
                                              bool DarkMode) : ICommand;

internal sealed class UpdateUserProfileCommandHandler(IUserContext userContext,
                                                      IUnitOfWork unitOfWork,
                                                      IUserRepository userRepository) : ICommandHandler<UpdateUserProfileCommand>
{
    public async ValueTask<Result> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userId = userContext.UserId;
        if (userId <= 0)
            return Error.Unauthorized("user.unauthenticated", "Usuario no autenticado.");

        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null)
            return Error.NotFound("user.not_found", "Usuario no encontrado.");

        user.EnsureProfile(request.Language, request.DarkMode);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class UpdateUserProfileCommandValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileCommandValidator(IOptions<ApplicationSettings> options)
    {
        var supportedCultures = (options.Value.CultureInfo ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        RuleFor(x => x.Language)
            .NotEmpty()
            .MaximumLength(5)
            .Must(language => supportedCultures.Contains(language))
            .WithMessage($"El idioma debe ser uno de los admitidos: {string.Join(", ", supportedCultures)}.");
    }
}
