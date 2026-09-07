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
        var cs = CourseSubject.Create(1, 1);
        cs.EnrollStudent(1);

        var ex = Assert.Throws<InvalidOperationException>(() => cs.EnrollStudent(1));
        Assert.Contains("already", ex.Message.ToLower());
    }

    [Fact]
    public void EnrollStudent_Valid_Adds()
    {
        var cs = CourseSubject.Create(1, 1);
        cs.EnrollStudent(1);
        cs.EnrollStudent(2);

        Assert.Equal(2, cs.Students.Count);
    }
}
