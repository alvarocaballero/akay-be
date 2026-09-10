using Akay.Be.Domain.Entities.Academic;

namespace Akay.Be.Domain.Tests.Entities;

public class CourseTests
{

    [Fact]
    public void AddSubject_Duplicate_Throws()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.AddSubject(1);

        var ex = Assert.Throws<InvalidOperationException>(() => course.AddSubject(1));
        Assert.Contains("already", ex.Message.ToLower());
    }

    [Fact]
    public void AddSubject_Valid_Adds()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.AddSubject(1);
        course.AddSubject(2);

        Assert.Equal(2, course.Subjects.Count);
    }

    [Fact]
    public void EnrollStudent_Duplicate_Throws()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        var student = CreateStudent(1);
        course.EnrollStudent(student, null);

        var duplicateEnrollment = course.EnrollStudent(student, null);

        Assert.False(duplicateEnrollment.Changed);
    }

    [Fact]
    public void EnrollStudent_Valid_Adds()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.EnrollStudent(CreateStudent(1), null);
        course.EnrollStudent(CreateStudent(2), null);

        Assert.Equal(2, course.Students.Count);
    }

    [Fact]
    public void EnrollStudent_WithAllSubjects_AddsCompleteEnrollment()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.AddSubject(10);
        course.AddSubject(20);

        var enrollment = course.EnrollStudent(CreateStudent(100), null);

        Assert.True(enrollment.Changed);
        Assert.Equal(2, course.Subjects.Sum(subject => subject.Students.Count));
        Assert.All(course.Subjects, subject => Assert.Same(enrollment.StudentCourse, subject.Students.Single().StudentCourse));
    }

    [Fact]
    public void EnrollStudent_WithAdditionalSubjects_CompletesPartialEnrollment()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.AddSubject(10);
        course.AddSubject(20);

        var student = CreateStudent(100);
        course.EnrollStudent(student, [10]);
        var enrollment = course.EnrollStudent(student, [20]);

        Assert.True(enrollment.Changed);
        Assert.Single(course.Students);
        Assert.All(course.Subjects, subject => Assert.Single(subject.Students));
    }

    [Fact]
    public void UnenrollStudent_RemovesCourseAndSubjectEnrollments()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.AddSubject(10);
        course.AddSubject(20);
        var enrollment = course.EnrollStudent(CreateStudent(100), null);

        course.UnenrollStudent(enrollment.StudentCourse);

        Assert.Empty(course.Students);
        Assert.All(course.Subjects, subject => Assert.Empty(subject.Students));
    }

    private static Student CreateStudent(int userId)
    {
        var user = Akay.Be.Domain.Entities.Identity.User.Create($"student{userId}@example.com", "Student", "Test");
        typeof(Akay.Be.Domain.Entities.Identity.User).GetProperty(nameof(Akay.Be.Domain.Entities.Identity.User.Id))!.SetValue(user, userId);
        return Student.Create(user, 1);
    }
}
