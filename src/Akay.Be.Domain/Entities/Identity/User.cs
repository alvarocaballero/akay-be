using Akay.Be.Domain.Enums;
using Akay.Be.Domain.Events.Identity;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Identity;

public sealed class User : AggregateRoot<int>, IAuditable, ISoftDeletable
{
    private readonly List<UserRoleAssignment> _roleAssignments = [];

    private User() { }

    public Guid? ExternalId { get; private set; }
    public string Email { get; private set; } = default!;
    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public bool IsActive { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144
    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments.AsReadOnly();

    public static User Create(string email, string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        return new User
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            IsActive = true
        };
    }

    public void AssignGlobalRole(UserRole role)
    {
        if (role != UserRole.SuperAdmin)
            throw new InvalidOperationException($"Role {role} must be assigned to a center. Use AssignRole instead.");

        if (_roleAssignments.Any(r => r.CenterId == null && r.Role == role && r.DeletedAt == null))
            throw new InvalidOperationException($"User already has the global role {role}.");

        var assignment = new UserRoleAssignment(Id, null, role);
        _roleAssignments.Add(assignment);
    }

    public void AssignRole(int centerId, UserRole role)
    {
        if (role == UserRole.SuperAdmin)
            throw new InvalidOperationException("SuperAdmin is a global role and cannot be assigned to a center.");

        if (centerId <= 0)
            throw new ArgumentException("CenterId must be greater than zero.", nameof(centerId));

        if (_roleAssignments.Any(r => r.CenterId == centerId && r.Role == role && r.DeletedAt == null))
            throw new InvalidOperationException($"User already has role {role} in center {centerId}.");

        var assignment = new UserRoleAssignment(Id, centerId, role);
        _roleAssignments.Add(assignment);
    }

    public void RemoveRole(int? centerId, UserRole role)
    {
        var assignment = _roleAssignments.FirstOrDefault(r => r.CenterId == centerId && r.Role == role && r.DeletedAt == null)
            ?? throw new InvalidOperationException($"User does not have role {role} for the specified center.");

        assignment.SoftDelete();
    }

    public void UpdateProfile(string email, string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var emailChanged = !string.Equals(Email, email, StringComparison.OrdinalIgnoreCase);
        var previousEmail = Email;
        var previousExternalId = ExternalId;

        Email = email;
        FirstName = firstName;
        LastName = lastName;

        if (emailChanged && previousExternalId.HasValue)
        {
            RaiseDomainEvent(new ExternalIdentityCleanupRequestedOutboxEvent(previousExternalId.Value,
                                                                             previousEmail,
                                                                             ExternalIdentityCleanupReasons.EmailChanged));
            ExternalId = null;
        }
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void SoftDelete()
    {
        if (DeletedAt is not null)
            return;

        if (ExternalId.HasValue)
        {
            RaiseDomainEvent(new ExternalIdentityCleanupRequestedOutboxEvent(ExternalId.Value,
                                                                             Email,
                                                                             ExternalIdentityCleanupReasons.LocalUserDeleted));
        }

        DeletedAt = DateTimeOffset.UtcNow;
    }

    public void SetExternalId(Guid externalId)
    {
        if (externalId == Guid.Empty)
            throw new ArgumentException("ExternalId cannot be empty.", nameof(externalId));

        ExternalId = externalId;
    }
}
