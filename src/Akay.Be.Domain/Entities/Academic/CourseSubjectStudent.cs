using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class CourseSubjectStudent : Entity<int>, IAuditable, ISoftDeletable
{
    private CourseSubjectStudent() { }

    public int CourseSubjectId { get; private set; }
    public CourseSubject CourseSubject { get; private set; } = default!;
    public int StudentCourseId { get; private set; }
    public StudentCourse StudentCourse { get; private set; } = default!;
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    internal static CourseSubjectStudent Create(int courseSubjectId, int studentCourseId)
    {
        if (studentCourseId <= 0)
            throw new ArgumentException("StudentCourseId must be greater than zero.", nameof(studentCourseId));

        return new CourseSubjectStudent
        {
            CourseSubjectId = courseSubjectId,
            StudentCourseId = studentCourseId
        };
    }

    internal void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
