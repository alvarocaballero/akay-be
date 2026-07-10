using Akay.Be.Domain.Entities.Identity;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class SubjectAdmin : Entity<int>, IAuditable, ISoftDeletable
{
    private SubjectAdmin() { }

    public int SubjectId { get; private set; }
    public Subject Subject { get; private set; } = default!;
    public int UserId { get; private set; }
    public User User { get; private set; } = default!;
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    internal static SubjectAdmin Create(int subjectId, int userId)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be greater than zero.", nameof(userId));

        return new SubjectAdmin
        {
            SubjectId = subjectId,
            UserId = userId
        };
    }

    internal void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
