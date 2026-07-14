using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Events.Academic;

namespace Akay.Be.Domain.Tests.Entities;

public class AcademicPeriodTests
{
    [Fact]
    public void Create_InvalidDates_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            AcademicPeriod.Create(1, "2026-2027", new DateOnly(2027, 6, 30), new DateOnly(2026, 9, 1)));
        Assert.Contains("StartDate", ex.Message);
    }

    [Fact]
    public void Create_EqualDates_Throws()
    {
        var date = new DateOnly(2026, 9, 1);
        var ex = Assert.Throws<ArgumentException>(() =>
            AcademicPeriod.Create(1, "2026-2027", date, date));
        Assert.Contains("StartDate", ex.Message);
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            AcademicPeriod.Create(1, " ", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        Assert.Contains("name", ex.Message.ToLower());
    }

    [Fact]
    public void Create_ZeroCenterId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            AcademicPeriod.Create(0, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30)));
        Assert.Contains("CenterId", ex.Message);
    }

    [Fact]
    public void Create_Valid_SetsProperties()
    {
        var period = AcademicPeriod.Create(1, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));

        Assert.Equal(1, period.CenterId);
        Assert.NotEqual(Guid.Empty, period.SyncId);
        Assert.Equal("2026-2027", period.Name);
        Assert.Equal(new DateOnly(2026, 9, 1), period.StartDate);
        Assert.Equal(new DateOnly(2027, 6, 30), period.EndDate);
        Assert.True(period.IsActive);

        var outboxEvent = Assert.IsType<AcademicPeriodCreatedOutboxEvent>(Assert.Single(period.AfterSaveDomainEvents));
        Assert.Equal(period.SyncId, outboxEvent.SyncId);
        Assert.Equal(period.CenterId, outboxEvent.CenterId);
        Assert.Equal(period.Name, outboxEvent.Name);
    }

    [Fact]
    public void ChangeDates_ValidDates_Updates()
    {
        var period = AcademicPeriod.Create(1, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));

        period.ChangeDates(new DateOnly(2026, 10, 1), new DateOnly(2027, 7, 31));

        Assert.Equal(new DateOnly(2026, 10, 1), period.StartDate);
        Assert.Equal(new DateOnly(2027, 7, 31), period.EndDate);
    }

    [Fact]
    public void ChangeDates_InvalidDates_Throws()
    {
        var period = AcademicPeriod.Create(1, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));

        var ex = Assert.Throws<ArgumentException>(() =>
            period.ChangeDates(new DateOnly(2027, 6, 30), new DateOnly(2026, 9, 1)));
        Assert.Contains("StartDate", ex.Message);
    }

    [Fact]
    public void Activate_SetsIsActive_And_Raises_DomainEvent()
    {
        var period = AcademicPeriod.Create(1, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        period.Deactivate();
        period.ClearDomainEvents();

        period.Activate();

        Assert.True(period.IsActive);
        var domainEvent = Assert.IsType<AcademicPeriodActivatedDomainEvent>(Assert.Single(period.AfterSaveDomainEvents));
        Assert.Equal(period.SyncId, domainEvent.SyncId);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var period = AcademicPeriod.Create(1, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        period.Deactivate();

        Assert.False(period.IsActive);
    }

    [Fact]
    public void Update_DoesNotRaiseOutboxEvents()
    {
        var period = AcademicPeriod.Create(1, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        period.ClearDomainEvents();

        period.Update("2026-2028",
                      new DateOnly(2026, 10, 1),
                      new DateOnly(2027, 7, 31),
                      false);

        Assert.Empty(period.AfterSaveDomainEvents);
    }
}
