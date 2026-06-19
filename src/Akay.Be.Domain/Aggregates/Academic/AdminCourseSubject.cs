using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;
using Akay.Be.Domain.Aggregates.Identity;

namespace Akay.Be.Domain.Aggregates.Academic;

public class AdminCourseSubject : Entity<int>, ISoftDeletable, IAuditable
{
    public int CourseSubjectId { get; private set; }

    public int UserId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public CourseSubject CourseSubject { get; private set; } = null!;

    public User User { get; private set; } = null!;

    private AdminCourseSubject()
    {
    }

    public static AdminCourseSubject Create(CourseSubject courseSubject, User user)
    {
        ArgumentNullException.ThrowIfNull(courseSubject);
        ArgumentNullException.ThrowIfNull(user);

        return new AdminCourseSubject
        {
            CourseSubjectId = courseSubject.Id,
            CourseSubject = courseSubject,
            UserId = user.Id,
            User = user,
            IsActive = true,
        };
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
