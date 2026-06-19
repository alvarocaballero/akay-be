using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Aggregates.Academic;

public class StudentCourse : Entity<int>, ISoftDeletable, IAuditable
{
    private readonly List<StudentCourseSubject> _studentCourseSubjects = [];

    public int StudentId { get; private set; }

    public int CourseId { get; private set; }

    public DateTime EnrolledAt { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Student Student { get; private set; } = null!;

    public Course Course { get; private set; } = null!;

    public IReadOnlyCollection<StudentCourseSubject> StudentCourseSubjects =>
        _studentCourseSubjects.AsReadOnly();

    private StudentCourse()
    {
    }

    public static StudentCourse Create(Student student, Course course, DateTime enrolledAt)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentNullException.ThrowIfNull(course);

        if (student.CenterId != course.CenterId)
        {
            throw new InvalidOperationException("Course must belong to the student's center.");
        }

        return new StudentCourse
        {
            StudentId = student.Id,
            CourseId = course.Id,
            EnrolledAt = enrolledAt,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;
}
