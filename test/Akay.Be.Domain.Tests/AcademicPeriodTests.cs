using Akay.Be.Domain.Aggregates.Academic;
using Akay.Be.Domain.Aggregates.Organization;

namespace Akay.Be.Domain.Tests;

public class AcademicPeriodTests
{
    [Fact]
    public void CreateValidPeriodSetsProperties()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 12, 31);

        var period = AcademicPeriod.Create(center, "2026", start, end);

        Assert.Equal(center.Id, period.CenterId);
        Assert.Equal("2026", period.Name);
        Assert.Equal(start, period.StartDate);
        Assert.Equal(end, period.EndDate);
        Assert.True(period.IsActive);
    }

    [Fact]
    public void CreateOnRootOrganizationThrows()
    {
        var root = TestDataFactory.CreateRootOrganization(Guid.NewGuid());
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 12, 31);

        Assert.Throws<ArgumentException>(() =>
            AcademicPeriod.Create(root, "P", start, end));
    }

    [Fact]
    public void CreateEndBeforeStartThrows()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var start = new DateOnly(2026, 12, 31);
        var end = new DateOnly(2026, 1, 1);

        Assert.Throws<ArgumentException>(() =>
            AcademicPeriod.Create(center, "Invalid", start, end));
    }

    [Fact]
    public void CreateEndEqualToStartSucceeds()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var date = new DateOnly(2026, 6, 1);

        var period = AcademicPeriod.Create(center, "Same", date, date);

        Assert.Equal(date, period.StartDate);
        Assert.Equal(date, period.EndDate);
    }

    [Fact]
    public void UpdateDatesWithEndBeforeStartThrows()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = AcademicPeriod.Create(center, "P",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));

        Assert.Throws<ArgumentException>(() =>
            period.UpdateDates(new DateOnly(2026, 12, 31), new DateOnly(2026, 1, 1)));
    }
}
