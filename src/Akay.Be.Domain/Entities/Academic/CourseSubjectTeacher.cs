using Akay.Be.Domain.Entities.Identity;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class CourseSubjectTeacher : Entity<int>, IAuditable, ISoftDeletable
{
    private CourseSubjectTeacher() { }

    public int CourseSubjectId { get; private set; }
    public CourseSubject CourseSubject { get; private set; } = default!;
    public int UserId { get; private set; }
    public User User { get; private set; } = default!;
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    internal static CourseSubjectTeacher Create(int courseSubjectId, int userId)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be greater than zero.", nameof(userId));

        return new CourseSubjectTeacher
        {
            CourseSubjectId = courseSubjectId,
            UserId = userId
        };
    }

    internal void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
