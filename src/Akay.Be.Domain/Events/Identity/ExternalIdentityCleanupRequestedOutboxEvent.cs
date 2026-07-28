using Akay.To.Core.Domain.Events;

namespace Akay.Be.Domain.Events.Identity;

public sealed record ExternalIdentityCleanupRequestedOutboxEvent(Guid ExternalId,
                                                                 string Email,
                                                                 string Reason) : IOutboxDomainEvent;

public static class ExternalIdentityCleanupReasons
{
    public const string LocalUserDeleted = "LocalUserDeleted";
    public const string EmailChanged = "EmailChanged";
    public const string NoLocalUser = "NoLocalUser";
}
