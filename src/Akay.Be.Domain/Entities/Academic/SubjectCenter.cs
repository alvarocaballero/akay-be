using Akay.Be.Domain.Entities.Organization;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class SubjectCenter : Entity<int>, IAuditable, ISoftDeletable
{
    private SubjectCenter() { }

    public int SubjectId { get; private set; }
    public Subject Subject { get; private set; } = default!;
    public int CenterId { get; private set; }
    public Center Center { get; private set; } = default!;
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    internal static SubjectCenter Create(int centerId)
    {
        if (centerId <= 0)
            throw new ArgumentException("CenterId must be greater than zero.", nameof(centerId));

        return new SubjectCenter
        {
            CenterId = centerId
        };
    }

    internal static SubjectCenter Create(int subjectId, int centerId)
    {
        if (subjectId <= 0)
            throw new ArgumentException("SubjectId must be greater than zero.", nameof(subjectId));

        if (centerId <= 0)
            throw new ArgumentException("CenterId must be greater than zero.", nameof(centerId));

        return new SubjectCenter
        {
            SubjectId = subjectId,
            CenterId = centerId
        };
    }

    internal void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
