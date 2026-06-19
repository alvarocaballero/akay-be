using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Aggregates.Academic;

public class CourseSubject : Entity<int>, ISoftDeletable, IAuditable
{
    private readonly List<AdminCourseSubject> _adminCourseSubjects = [];
    private readonly List<StudentCourseSubject> _studentCourseSubjects = [];

    public int CourseId { get; private set; }

    public int SubjectId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Course Course { get; private set; } = null!;

    public Subject Subject { get; private set; } = null!;

    public IReadOnlyCollection<AdminCourseSubject> AdminCourseSubjects =>
        _adminCourseSubjects.AsReadOnly();

    public IReadOnlyCollection<StudentCourseSubject> StudentCourseSubjects =>
        _studentCourseSubjects.AsReadOnly();

    private CourseSubject()
    {
    }

    public static CourseSubject Create(Course course, Subject subject)
    {
        ArgumentNullException.ThrowIfNull(course);
        ArgumentNullException.ThrowIfNull(subject);

        return new CourseSubject
        {
            CourseId = course.Id,
            Course = course,
            SubjectId = subject.Id,
            Subject = subject,
            IsActive = true,
        };
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
