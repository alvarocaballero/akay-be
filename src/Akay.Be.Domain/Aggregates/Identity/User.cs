using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Aggregates.Identity;

public class User : AggregateRoot<int>, ISoftDeletable, IAuditable
{
    private readonly List<UserRoleAssignment> _userRoleAssignments = [];

    public string ExternalId { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public IReadOnlyCollection<UserRoleAssignment> UserRoleAssignments =>
        _userRoleAssignments.AsReadOnly();

    private User()
    {
    }

    public static User Create(string externalId, string email, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (externalId.Length > 100)
        {
            throw new ArgumentException("ExternalId must be 100 characters or fewer.", nameof(externalId));
        }

        if (email.Length > 320)
        {
            throw new ArgumentException("Email must be 320 characters or fewer.", nameof(email));
        }

        if (displayName.Length > 200)
        {
            throw new ArgumentException("DisplayName must be 200 characters or fewer.", nameof(displayName));
        }

        return new User
        {
            ExternalId = externalId,
            Email = email,
            DisplayName = displayName,
            IsActive = true,
        };
    }

    public void UpdateDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (displayName.Length > 200)
        {
            throw new ArgumentException("DisplayName must be 200 characters or fewer.", nameof(displayName));
        }

        DisplayName = displayName;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
