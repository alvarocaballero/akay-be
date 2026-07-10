using Akay.Be.Domain.Entities.Identity;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class Student : AggregateRoot<int>, IAuditable, ISoftDeletable
{
    private Student() { }

    public int UserId { get; private set; }
    public User User { get; private set; } = default!;
    public int CenterId { get; private set; }
    public string? StudentNumber { get; private set; }
    public bool IsActive { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    public static Student Create(int userId, int centerId, string? studentNumber = null)
    {
        if (userId <= 0)
            throw new ArgumentException("UserId must be greater than zero.", nameof(userId));

        if (centerId <= 0)
            throw new ArgumentException("CenterId must be greater than zero.", nameof(centerId));

        return new Student
        {
            UserId = userId,
            CenterId = centerId,
            StudentNumber = studentNumber,
            IsActive = true
        };
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void ChangeStudentNumber(string? studentNumber)
    {
        StudentNumber = studentNumber;
    }

    public void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
