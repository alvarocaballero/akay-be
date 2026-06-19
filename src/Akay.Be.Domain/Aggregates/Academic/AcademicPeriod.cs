using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;
using Akay.Be.Domain.Aggregates.Organization;

namespace Akay.Be.Domain.Aggregates.Academic;

public class AcademicPeriod : AggregateRoot<int>, ISoftDeletable, IAuditable
{
    public int CenterId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public Organization.Organization Center { get; private set; } = null!;

    private AcademicPeriod()
    {
    }

    public static AcademicPeriod Create(Organization.Organization center, string name, DateOnly startDate, DateOnly endDate)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!center.IsCenter)
        {
            throw new ArgumentException("AcademicPeriod must be associated to a center organization (IsCenter = true).", nameof(center));
        }

        if (name.Length > 100)
        {
            throw new ArgumentException("AcademicPeriod name must be 100 characters or fewer.", nameof(name));
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("EndDate must be greater than or equal to StartDate.", nameof(endDate));
        }

        return new AcademicPeriod
        {
            CenterId = center.Id,
            Center = center,
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true,
        };
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100)
        {
            throw new ArgumentException("AcademicPeriod name must be 100 characters or fewer.", nameof(name));
        }

        Name = name;
    }

    public void UpdateDates(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("EndDate must be greater than or equal to StartDate.", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
