using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;
using Akay.Be.Domain.Aggregates.Organization;

namespace Akay.Be.Domain.Aggregates.Academic;

public class Subject : AggregateRoot<int>, ISoftDeletable, IAuditable
{
    private readonly List<CourseSubject> _courseSubjects = [];

    public int? OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Version { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Organization.Organization? Organization { get; private set; }

    public IReadOnlyCollection<CourseSubject> CourseSubjects =>
        _courseSubjects.AsReadOnly();

    private Subject()
    {
    }

    public static Subject Create(string code, string name, Organization.Organization? organization = null, string? version = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (code.Length > 50)
        {
            throw new ArgumentException("Subject code must be 50 characters or fewer.", nameof(code));
        }

        if (name.Length > 200)
        {
            throw new ArgumentException("Subject name must be 200 characters or fewer.", nameof(name));
        }

        if (version is { Length: > 100 })
        {
            throw new ArgumentException("Subject version must be 100 characters or fewer.", nameof(version));
        }

        return new Subject
        {
            Code = code,
            Name = name,
            Version = version,
            OrganizationId = organization?.Id,
            Organization = organization,
            IsActive = true,
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 200)
        {
            throw new ArgumentException("Subject name must be 200 characters or fewer.", nameof(name));
        }

        Name = name;
    }

    public void SetVersion(string? version)
    {
        if (version is { Length: > 100 })
        {
            throw new ArgumentException("Subject version must be 100 characters or fewer.", nameof(version));
        }

        Version = version;
    }

    public void AssignToOrganization(Organization.Organization? organization)
    {
        OrganizationId = organization?.Id;
        Organization = organization;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
