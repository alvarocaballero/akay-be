using Akay.Be.Application.Abstractions.Identity;

namespace Akay.Be.Infrastructure.Identity;

/// <summary>
/// Implementación temporal de <see cref="IIdentityProvisioningService"/>
/// que no interactúa con ningún proveedor de identidad.
/// Genera un identificador externo ficticio para permitir el desarrollo local
/// hasta que se configure Microsoft Entra ID.
/// </summary>
internal sealed class NoOpIdentityProvisioningService : IIdentityProvisioningService
{
    public Task<Guid> CreateUserAsync(string email, string firstName, string lastName, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Guid.NewGuid());
    }

    public Task UpdateUserAsync(Guid externalId, string email, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task DeactivateUserAsync(Guid externalId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
