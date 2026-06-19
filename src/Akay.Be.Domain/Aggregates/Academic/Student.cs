using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;
using Akay.Be.Domain.Aggregates.Identity;
using Akay.Be.Domain.Aggregates.Organization;

namespace Akay.Be.Domain.Aggregates.Academic;

public class Student : Entity<int>, ISoftDeletable, IAuditable
{
    private readonly List<StudentCourse> _studentCourses = [];

    public int UserId { get; private set; }

    public int CenterId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public User User { get; private set; } = null!;

    public Organization.Organization Center { get; private set; } = null!;

    public IReadOnlyCollection<StudentCourse> StudentCourses =>
        _studentCourses.AsReadOnly();

    private Student()
    {
    }

    public static Student Create(int userId, Organization.Organization center)
    {
        if (userId <= 0)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        ArgumentNullException.ThrowIfNull(center);

        if (!center.IsCenter)
        {
            throw new ArgumentException("Student must be associated to a center organization (IsCenter = true).", nameof(center));
        }

        return new Student
        {
            UserId = userId,
            CenterId = center.Id,
            IsActive = true,
        };
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
