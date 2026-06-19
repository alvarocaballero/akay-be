using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Aggregates.Organization;

public class Organization : AggregateRoot<int>, IHasTenant, ISoftDeletable, IAuditable
{
    private readonly List<Identity.UserRoleAssignment> _userRoleAssignments = [];
    private readonly List<Academic.Course> _courses = [];

    public Guid TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsCenter { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public IReadOnlyCollection<Identity.UserRoleAssignment> UserRoleAssignments =>
        _userRoleAssignments.AsReadOnly();

    public IReadOnlyCollection<Academic.Course> Courses =>
        _courses.AsReadOnly();

    private Organization()
    {
    }

    public static Organization Create(Guid tenantId, string name, bool isCenter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 200)
        {
            throw new ArgumentException("Organization name must be 200 characters or fewer.", nameof(name));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }

        return new Organization
        {
            TenantId = tenantId,
            Name = name,
            IsCenter = isCenter,
            IsActive = true,
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 200)
        {
            throw new ArgumentException("Organization name must be 200 characters or fewer.", nameof(name));
        }

        Name = name;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
