using Akay.To.Core.Domain.Events;

namespace Akay.Be.Domain.Events.Identity;

/// <summary>
/// Hecho de negocio: se solicito la limpieza de una identidad externa. Se persiste en el outbox
/// para que el componente responsable pueda reaccionar a la solicitud.
/// </summary>
public sealed record ExternalIdentityCleanupRequestedOutboxEvent(Guid ExternalId,
                                                                 string Email,
                                                                 string Reason) : IOutboxDomainEvent; // IOutboxIntegrationEvent cuando se procesen

public static class ExternalIdentityCleanupReasons
{
    public const string LocalUserDeleted = "LocalUserDeleted";
    public const string EmailChanged = "EmailChanged";
    public const string NoLocalUser = "NoLocalUser";
}
