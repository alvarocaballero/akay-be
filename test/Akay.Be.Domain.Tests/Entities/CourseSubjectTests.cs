using Akay.Be.Domain.Entities.Academic;

namespace Akay.Be.Domain.Tests.Entities;

public class CourseSubjectTests
{
    [Fact]
    public void AssignTeacher_Duplicate_Throws()
    {
        var cs = CourseSubject.Create(1, 1);
        cs.AssignTeacher(1);

        var ex = Assert.Throws<InvalidOperationException>(() => cs.AssignTeacher(1));
        Assert.Contains("already", ex.Message.ToLower());
    }

    [Fact]
    public void AssignTeacher_Valid_Adds()
    {
        var cs = CourseSubject.Create(1, 1);
        cs.AssignTeacher(1);
        cs.AssignTeacher(2);

        Assert.Equal(2, cs.Teachers.Count);
    }

    [Fact]
    public void EnrollStudent_Duplicate_Throws()
    {
        var (cs, studentCourse) = CreateEnrollment(1);
        cs.EnrollStudent(studentCourse);

        Assert.False(cs.EnrollStudent(studentCourse));
    }

    [Fact]
    public void EnrollStudent_Valid_Adds()
    {
        var (cs, firstStudentCourse) = CreateEnrollment(1);
        var (_, secondStudentCourse) = CreateEnrollment(2);
        cs.EnrollStudent(firstStudentCourse);
        cs.EnrollStudent(secondStudentCourse);

        Assert.Equal(2, cs.Students.Count);
    }

    private static (CourseSubject CourseSubject, StudentCourse StudentCourse) CreateEnrollment(int userId)
    {
        var course = Course.Create(1, "Course", $"C{userId}");
        course.AddSubject(1);
        var user = Akay.Be.Domain.Entities.Identity.User.Create($"student{userId}@example.com", "Student", "Test");
        typeof(Akay.Be.Domain.Entities.Identity.User).GetProperty(nameof(Akay.Be.Domain.Entities.Identity.User.Id))!.SetValue(user, userId);
        var enrollment = course.EnrollStudent(Student.Create(user, 1), []);
        return (course.Subjects.Single(), enrollment.StudentCourse);
    }
}
