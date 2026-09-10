using Akay.Be.Domain.Entities.Identity;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class StudentCourse : Entity<int>, IAuditable, ISoftDeletable
{
    private StudentCourse() { }

#pragma warning disable S1144
    public int CourseId { get; private set; }
    public Course Course { get; private set; } = default!;
    public int UserId { get; private set; }
    public User User { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    internal static StudentCourse Create(Student student)
    {
        ArgumentNullException.ThrowIfNull(student);

        return new StudentCourse
        {
            User = student.User,
            UserId = student.UserId
        };
    }

}
