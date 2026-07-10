using Akay.Be.Domain.Enums;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Identity;

public sealed class UserRoleAssignment : Entity<int>, IAuditable, ISoftDeletable
{
    private UserRoleAssignment() { }

    internal UserRoleAssignment(int userId, int? centerId, UserRole role)
    {
        if (role == UserRole.SuperAdmin && centerId.HasValue)
            throw new InvalidOperationException("SuperAdmin cannot be assigned to a center.");

        if (role != UserRole.SuperAdmin && !centerId.HasValue)
            throw new InvalidOperationException($"Role {role} requires a center.");

        if (centerId.HasValue && centerId.Value <= 0)
            throw new ArgumentException("CenterId must be greater than zero.", nameof(centerId));

        UserId = userId;
        CenterId = centerId;
        Role = role;
    }

    public int UserId { get; private set; }
    public User User { get; private set; } = default!;
    public int? CenterId { get; private set; }
    public UserRole Role { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    internal void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
