using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;
using Akay.Be.Domain.Aggregates.Organization;

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

    public Role Role { get; private set; } = null!;

    public Organization.Organization? Organization { get; private set; }

    private UserRoleAssignment()
    {
    }

    public static UserRoleAssignment Create(User user, Role role, Organization.Organization? organization)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(role);

        ValidateRoleScope(role, organization);

        return new UserRoleAssignment
        {
            UserId = user.Id,
            User = user,
            RoleId = role.Id,
            Role = role,
            OrganizationId = organization?.Id,
            Organization = organization,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static void ValidateRoleScope(Role role, Organization.Organization? organization)
    {
        var isGlobal = organization is null;

        if (string.Equals(role.Code, RoleCodes.SuperAdmin, StringComparison.Ordinal))
        {
            if (!isGlobal)
            {
                throw new InvalidOperationException(
                    $"Role '{RoleCodes.SuperAdmin}' must have OrganizationId = null.");
            }

            return;
        }

        if (isGlobal)
        {
            throw new InvalidOperationException(
                $"Role '{role.Code}' must be assigned to a specific organization.");
        }

        if (string.Equals(role.Code, RoleCodes.OrganizationAdmin, StringComparison.Ordinal) && organization!.IsCenter)
        {
            throw new InvalidOperationException(
                $"Role '{RoleCodes.OrganizationAdmin}' can only be assigned to a root organization (IsCenter = false).");
        }

        if (string.Equals(role.Code, RoleCodes.CenterAdmin, StringComparison.Ordinal) && !organization!.IsCenter)
        {
            throw new InvalidOperationException(
                $"Role '{RoleCodes.CenterAdmin}' can only be assigned to a center organization (IsCenter = true).");
        }
    }
}
