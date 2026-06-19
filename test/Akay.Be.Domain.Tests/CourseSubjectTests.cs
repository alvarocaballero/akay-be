using Akay.Be.Domain.Aggregates.Academic;

namespace Akay.Be.Domain.Tests;

public class CourseSubjectTests
{
    [Fact]
    public void CreateSucceeds()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Course");
        var subject = TestDataFactory.CreateSubject();

        var courseSubject = CourseSubject.Create(course, subject);

        Assert.Equal(course.Id, courseSubject.CourseId);
        Assert.Equal(subject.Id, courseSubject.SubjectId);
    }
}
