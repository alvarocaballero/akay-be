using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Aggregates.Academic;

public class StudentCourseSubject : Entity<int>, ISoftDeletable, IAuditable
{
    public int StudentCourseId { get; private set; }

    public int CourseSubjectId { get; private set; }

    public DateTime EnrolledAt { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public StudentCourse StudentCourse { get; private set; } = null!;

    public CourseSubject CourseSubject { get; private set; } = null!;

    private StudentCourseSubject()
    {
    }

    public static StudentCourseSubject Create(StudentCourse studentCourse, CourseSubject courseSubject, DateTime enrolledAt)
    {
        ArgumentNullException.ThrowIfNull(studentCourse);
        ArgumentNullException.ThrowIfNull(courseSubject);

        if (courseSubject.CourseId != studentCourse.CourseId)
        {
            throw new InvalidOperationException("CourseSubject must belong to the same course as StudentCourse.");
        }

        return new StudentCourseSubject
        {
            StudentCourseId = studentCourse.Id,
            CourseSubjectId = courseSubject.Id,
            EnrolledAt = enrolledAt,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;
}
