using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Aggregates.Identity;

public class Role : Entity<int>, IAuditable
{
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    private Role()
    {
    }

    public static Role Create(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (code.Length > 50)
        {
            throw new ArgumentException("Role code must be 50 characters or fewer.", nameof(code));
        }

        if (name.Length > 100)
        {
            throw new ArgumentException("Role name must be 100 characters or fewer.", nameof(name));
        }

        return new Role
        {
            Code = code,
            Name = name,
        };
    }
}

public static class RoleCodes
{
    public const string SuperAdmin = "SuperAdmin";
    public const string OrganizationAdmin = "OrganizationAdmin";
    public const string CenterAdmin = "CenterAdmin";
    public const string Teacher = "Teacher";
    public const string Student = "Student";
}
