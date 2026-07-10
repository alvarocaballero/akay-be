using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Organization;

public sealed class Center : AggregateRoot<int>, IAuditable, ISoftDeletable
{
    private readonly List<Entities.Academic.AcademicPeriod> _academicPeriods = [];

    private Center() { }

    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public bool IsActive { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144
    public IReadOnlyCollection<Entities.Academic.AcademicPeriod> AcademicPeriods => _academicPeriods.AsReadOnly();

    public static Center Create(string name, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return new Center
        {
            Name = name,
            Code = code,
            IsActive = true
        };
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
