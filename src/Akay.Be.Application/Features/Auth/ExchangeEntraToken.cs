using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Domain.Events.Identity;
using Akay.To.Core.Application.Abstractions.Outbox;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using Akay.To.Core.Application.Security.Jwt;

namespace Akay.Be.Application.Features.Auth;

public sealed record ExchangeEntraTokenCommand(Guid ExternalId,
                                               string Email,
                                               string Name,
                                               string? GivenName,
                                               string? FamilyName) : ICommand<ExchangeAkayTokenResponse>;

public sealed record ExchangeAkayTokenResponse(string AccessToken,
                                               int ExpiresIn,
                                               string TokenType);

internal sealed class ExchangeEntraTokenCommandHandler(IUserRepository userRepository,
                                                         IUnitOfWork unitOfWork,
                                                         IOutboxEventWriter outboxEventWriter,
                                                         IJwtTokenGenerator jwtTokenGenerator) : ICommandHandler<ExchangeEntraTokenCommand, ExchangeAkayTokenResponse>
{
    public async ValueTask<Result<ExchangeAkayTokenResponse>> Handle(ExchangeEntraTokenCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userRepository.GetByExternalIdAsync(request.ExternalId, cancellationToken);

        if (user is null)
        {
            user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (user is null)
            {
                outboxEventWriter.Enqueue(new ExternalIdentityCleanupRequestedOutboxEvent(request.ExternalId,
                                                                                          request.Email,
                                                                                          ExternalIdentityCleanupReasons.NoLocalUser));
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return Error.Forbidden("auth.exchange.user_not_found", "No existe un usuario de Akay vinculado al token de Entra ID.");
            }
        }

        if (!user.IsActive || user.DeletedAt is not null)
            return Error.Forbidden("auth.exchange.user_inactive", "El usuario no está activo en Akay.");

        if (!user.ExternalId.HasValue || user.ExternalId.Value != request.ExternalId)
        {
            user.SetExternalId(request.ExternalId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var fullName = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        var roles = user.RoleAssignments
                        .Where(r => r.DeletedAt == null)
                        .Select(r => r.Role.ToString().ToLowerInvariant())
                        .Distinct()
                        .ToList();

        var token = jwtTokenGenerator.Generate(new JwtTokenRequest(user.Id,
                                                                   string.IsNullOrWhiteSpace(fullName) ? request.Name : fullName,
                                                                   user.Email,
                                                                   user.FirstName,
                                                                   user.LastName,
                                                                   roles));

        return new ExchangeAkayTokenResponse(token.AccessToken, token.ExpiresInSeconds, "Bearer");
    }
}
