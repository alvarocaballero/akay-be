using Akay.Be.Domain.Aggregates.Academic;

namespace Akay.Be.Domain.Tests;

public class CourseTests
{
    [Fact]
    public void CreateValidCourseSetsProperties()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);

        var course = Course.Create(center, period, "Math 101");

        Assert.Equal(center.Id, course.CenterId);
        Assert.Equal(period.Id, course.AcademicPeriodId);
        Assert.Equal("Math 101", course.Name);
    }

    [Fact]
    public void CreateOnRootOrganizationThrows()
    {
        var root = TestDataFactory.CreateRootOrganization(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(TestDataFactory.CreateCenter(Guid.NewGuid()));

        Assert.Throws<ArgumentException>(() =>
            Course.Create(root, period, "X"));
    }

    [Fact]
    public void CreateWithDifferentCenterThanPeriodThrows()
    {
        var center1 = TestDataFactory.CreateCenter(Guid.NewGuid());
        TestDataFactory.SetId(center1, 1);
        var center2 = TestDataFactory.CreateCenter(Guid.NewGuid());
        TestDataFactory.SetId(center2, 2);
        var period2 = TestDataFactory.CreateAcademicPeriod(center2);
        TestDataFactory.SetId(period2, 100);

        Assert.Throws<InvalidOperationException>(() =>
            Course.Create(center1, period2, "X"));
    }

    [Fact]
    public void RenameUpdatesName()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Old");

        course.Rename("New");

        Assert.Equal("New", course.Name);
    }

    [Fact]
    public void DeactivateSetsIsActiveFalse()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Course");

        course.Deactivate();

        Assert.False(course.IsActive);
    }

    [Fact]
    public void ActivateSetsIsActiveTrue()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Course");
        course.Deactivate();

        course.Activate();

        Assert.True(course.IsActive);
    }
}
