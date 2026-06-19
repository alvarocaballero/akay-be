using Akay.Be.Domain.Aggregates.Academic;

namespace Akay.Be.Domain.Tests;

public class StudentCourseTests
{
    [Fact]
    public void CreateSameCenterSucceeds()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Course");
        var student = Student.Create(userId: 1, center);

        var sc = StudentCourse.Create(student, course, DateTime.UtcNow);

        Assert.Equal(student.Id, sc.StudentId);
        Assert.Equal(course.Id, sc.CourseId);
    }

    [Fact]
    public void CreateDifferentCenterThrows()
    {
        var center1 = TestDataFactory.CreateCenter(Guid.NewGuid());
        TestDataFactory.SetId(center1, 1);
        var center2 = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period2 = TestDataFactory.CreateAcademicPeriod(center2);
        var course2 = Course.Create(center2, period2, "Course2");
        TestDataFactory.SetId(course2, 100);
        var student = Student.Create(userId: 1, center1);
        TestDataFactory.SetId(student, 200);

        Assert.Throws<InvalidOperationException>(() =>
            StudentCourse.Create(student, course2, DateTime.UtcNow));
    }
}

public class StudentCourseSubjectTests
{
    [Fact]
    public void CreateSameCourseSucceeds()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var course = Course.Create(center, period, "Course");
        var subject = TestDataFactory.CreateSubject();
        var courseSubject = CourseSubject.Create(course, subject);
        var student = Student.Create(userId: 1, center);
        var sc = StudentCourse.Create(student, course, DateTime.UtcNow);

        var scs = StudentCourseSubject.Create(sc, courseSubject, DateTime.UtcNow);

        Assert.Equal(sc.Id, scs.StudentCourseId);
        Assert.Equal(courseSubject.Id, scs.CourseSubjectId);
    }

    [Fact]
    public void CreateDifferentCourseThrows()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());
        var period = TestDataFactory.CreateAcademicPeriod(center);
        var courseA = Course.Create(center, period, "CourseA");
        TestDataFactory.SetId(courseA, 1);
        var courseB = Course.Create(center, period, "CourseB");
        TestDataFactory.SetId(courseB, 2);
        var subjectB = TestDataFactory.CreateSubject(code: "B", name: "SubjB");
        var courseSubjectB = CourseSubject.Create(courseB, subjectB);
        var student = Student.Create(userId: 1, center);
        var sc = StudentCourse.Create(student, courseA, DateTime.UtcNow);
        TestDataFactory.SetId(sc, 500);

        Assert.Throws<InvalidOperationException>(() =>
            StudentCourseSubject.Create(sc, courseSubjectB, DateTime.UtcNow));
    }
}
