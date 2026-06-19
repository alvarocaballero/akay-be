using Akay.Be.Domain.Enums;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Aggregates.Identity;

public class UserRoleAssignment : Entity<int>, ISoftDeletable, IAuditable
{
    public int UserId { get; private set; }

    public int RoleId { get; private set; }

    public int? OrganizationId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public User User { get; private set; } = null!;

    public Organization.Organization? Organization { get; private set; }

    private UserRoleAssignment()
    {
    }

    public static UserRoleAssignment Create(User user, UserRole role, Organization.Organization? organization)
    {
        ArgumentNullException.ThrowIfNull(user);

        ValidateRoleScope(role, organization);

        return new UserRoleAssignment
        {
            UserId = user.Id,
            User = user,
            RoleId = (int)role,
            OrganizationId = organization?.Id,
            Organization = organization,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static void ValidateRoleScope(UserRole role, Organization.Organization? organization)
    {
        var isGlobal = organization is null;

        if (role == UserRole.SuperAdmin)
        {
            if (!isGlobal)
            {
                throw new InvalidOperationException(
                    $"Role '{nameof(UserRole.SuperAdmin)}' must have OrganizationId = null.");
            }

            return;
        }

        if (isGlobal)
        {
            throw new InvalidOperationException(
                $"Role '{role}' must be assigned to a specific organization.");
        }
    }
}
