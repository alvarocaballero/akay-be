using Akay.Be.Domain.Entities.Organization;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class AcademicPeriod : AggregateRoot<int>, IAuditable, ISoftDeletable
{
    private readonly List<Course> _courses = [];

    private AcademicPeriod() { }

    public int CenterId { get; private set; }
    public Center Center { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsActive { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144
    public IReadOnlyCollection<Course> Courses => _courses.AsReadOnly();

    public static AcademicPeriod Create(int centerId, string name, DateOnly startDate, DateOnly endDate)
    {
        if (centerId <= 0)
            throw new ArgumentException("CenterId must be greater than zero.", nameof(centerId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (startDate >= endDate)
            throw new ArgumentException("StartDate must be earlier than EndDate.");

        return new AcademicPeriod
        {
            CenterId = centerId,
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true
        };
    }

    public void ChangeDates(DateOnly startDate, DateOnly endDate)
    {
        if (startDate >= endDate)
            throw new ArgumentException("StartDate must be earlier than EndDate.");

        StartDate = startDate;
        EndDate = endDate;
    }

    public void ChangeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
