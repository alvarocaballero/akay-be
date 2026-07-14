using Akay.Be.Domain.Entities.Organization;
using Akay.Be.Domain.Events.Academic;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;
using Akay.To.Core.Domain.Events;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class AcademicPeriod : AggregateRoot<int>, IAuditable, ISoftDeletable, IHasSyncId
{
    private readonly List<Course> _courses = [];

    private AcademicPeriod() { }

    public int CenterId { get; private set; }
    public Guid SyncId { get; private set; }
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

        var period = new AcademicPeriod
        {
            CenterId = centerId,
            SyncId = Guid.CreateVersion7(),
            Name = name,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true
        };

        period.RaiseDomainEvent(new AcademicPeriodCreatedOutboxEvent(period.SyncId,
                                                                     period.CenterId,
                                                                     period.Name,
                                                                     period.StartDate,
                                                                     period.EndDate,
                                                                     period.IsActive));

        return period;
    }

    public void ChangeDates(DateOnly startDate, DateOnly endDate)
    {
        if (startDate >= endDate)
            throw new ArgumentException("StartDate must be earlier than EndDate.");

        if (StartDate == startDate && EndDate == endDate)
            return;

        StartDate = startDate;
        EndDate = endDate;
    }

    public void ChangeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Name == name)
            return;

        Name = name;
    }

    public void Update(string name,
                       DateOnly startDate,
                       DateOnly endDate,
                       bool isActive)
    {
        ChangeName(name);
        ChangeDates(startDate, endDate);

        if (isActive)
            Activate();
        else
            Deactivate();
    }

    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        RaiseDomainEvent(new AcademicPeriodActivatedDomainEvent(SyncId, CenterId, Name));
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        RaiseDomainEvent(new AcademicPeriodDeactivatedDomainEvent(SyncId, CenterId, Name), DomainEventTiming.BeforeSave);
    }

    public void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
    }
}
