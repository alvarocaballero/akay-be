using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;
using Akay.Be.Domain.Aggregates.Organization;

namespace Akay.Be.Domain.Aggregates.Academic;

public class Course : AggregateRoot<int>, ISoftDeletable, IAuditable
{
    private readonly List<CourseSubject> _courseSubjects = [];
    private readonly List<StudentCourse> _studentCourses = [];

    public int CenterId { get; private set; }

    public int AcademicPeriodId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Organization.Organization Center { get; private set; } = null!;

    public AcademicPeriod AcademicPeriod { get; private set; } = null!;

    public IReadOnlyCollection<CourseSubject> CourseSubjects =>
        _courseSubjects.AsReadOnly();

    public IReadOnlyCollection<StudentCourse> StudentCourses =>
        _studentCourses.AsReadOnly();

    private Course()
    {
    }

    public static Course Create(Organization.Organization center, AcademicPeriod academicPeriod, string name)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(academicPeriod);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!center.IsCenter)
        {
            throw new ArgumentException("Course must be associated to a center organization (IsCenter = true).", nameof(center));
        }

        if (name.Length > 200)
        {
            throw new ArgumentException("Course name must be 200 characters or fewer.", nameof(name));
        }

        if (center.Id != academicPeriod.CenterId)
        {
            throw new InvalidOperationException("Course and AcademicPeriod must belong to the same center.");
        }

        return new Course
        {
            CenterId = center.Id,
            Center = center,
            AcademicPeriodId = academicPeriod.Id,
            AcademicPeriod = academicPeriod,
            Name = name,
            IsActive = true,
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 200)
        {
            throw new ArgumentException("Course name must be 200 characters or fewer.", nameof(name));
        }

        Name = name;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
