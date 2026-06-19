using Akay.Be.Domain.Aggregates.Academic;

namespace Akay.Be.Domain.Tests;

public class AdminCourseSubjectTests
{
    [Fact]
    public void CreateValidAdminCourseSubjectSetsProperties()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Course");
        var subject = TestDataFactory.CreateSubject();
        var courseSubject = CourseSubject.Create(course, subject);
        var user = TestDataFactory.CreateUser();

        var acs = AdminCourseSubject.Create(courseSubject, user);

        Assert.Equal(courseSubject.Id, acs.CourseSubjectId);
        Assert.Equal(user.Id, acs.UserId);
        Assert.True(acs.IsActive);
    }

    [Fact]
    public void CreateWithNullCourseSubjectThrows()
    {
        var user = TestDataFactory.CreateUser();

        Assert.Throws<ArgumentNullException>(() =>
            AdminCourseSubject.Create(null!, user));
    }

    [Fact]
    public void CreateWithNullUserThrows()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Course");
        var subject = TestDataFactory.CreateSubject();
        var courseSubject = CourseSubject.Create(course, subject);

        Assert.Throws<ArgumentNullException>(() =>
            AdminCourseSubject.Create(courseSubject, null!));
    }

    [Fact]
    public void DeactivateSetsIsActiveFalse()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Course");
        var subject = TestDataFactory.CreateSubject();
        var courseSubject = CourseSubject.Create(course, subject);
        var user = TestDataFactory.CreateUser();
        var acs = AdminCourseSubject.Create(courseSubject, user);

        acs.Deactivate();

        Assert.False(acs.IsActive);
    }

    [Fact]
    public void ActivateSetsIsActiveTrue()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Course");
        var subject = TestDataFactory.CreateSubject();
        var courseSubject = CourseSubject.Create(course, subject);
        var user = TestDataFactory.CreateUser();
        var acs = AdminCourseSubject.Create(courseSubject, user);
        acs.Deactivate();

        acs.Activate();

        Assert.True(acs.IsActive);
    }
}
