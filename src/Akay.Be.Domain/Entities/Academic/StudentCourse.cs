using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class StudentCourse : Entity<int>, IAuditable, ISoftDeletable
{
    private StudentCourse() { }

    public int CourseId { get; private set; }
    public Course Course { get; private set; } = default!;
    public int StudentId { get; private set; }
    public Student Student { get; private set; } = default!;
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    internal static StudentCourse Create(int courseId, int studentId)
    {
        if (studentId <= 0)
            throw new ArgumentException("StudentId must be greater than zero.", nameof(studentId));

        return new StudentCourse
        {
            CourseId = courseId,
            StudentId = studentId
        };
    }

    internal void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
