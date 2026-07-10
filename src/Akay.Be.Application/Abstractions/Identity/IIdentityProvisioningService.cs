namespace Akay.Be.Application.Abstractions.Identity;

/// <summary>
/// Abstracción para provisionar/desactivar usuarios en el proveedor de identidad externo.
/// Permite preparar Akay.Be para Entra ID sin acoplar la aplicación a Microsoft Graph.
/// </summary>
public interface IIdentityProvisioningService
{
    /// <summary>
    /// Crea un usuario en el proveedor de identidad.
    /// </summary>
    /// <param name="email">Correo electrónico del usuario.</param>
    /// <param name="firstName">Nombre.</param>
    /// <param name="lastName">Apellidos.</param>
    /// <param name="temporaryPassword">Contraseña temporal generada.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Identificador externo asignado por el proveedor de identidad.</returns>
    Task<Guid> CreateUserAsync(string email, string firstName, string lastName, string temporaryPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los datos básicos del usuario en el proveedor de identidad.
    /// </summary>
    Task UpdateUserAsync(Guid externalId, string email, string firstName, string lastName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Desactiva el usuario en el proveedor de identidad.
    /// </summary>
    Task DeactivateUserAsync(Guid externalId, CancellationToken cancellationToken = default);
}
