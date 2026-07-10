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

        Assert.Equal(2, cs.Teachers.Count(t => t.DeletedAt == null));
    }

    [Fact]
    public void RemoveTeacher_Valid_Removes()
    {
        var cs = CourseSubject.Create(1, 1);
        cs.AssignTeacher(1);
        cs.RemoveTeacher(1);

        Assert.DoesNotContain(cs.Teachers, t => t.DeletedAt == null);
    }

    [Fact]
    public void RemoveTeacher_NonExistent_Throws()
    {
        var cs = CourseSubject.Create(1, 1);

        var ex = Assert.Throws<InvalidOperationException>(() => cs.RemoveTeacher(99));
        Assert.Contains("not assigned", ex.Message.ToLower());
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

        Assert.Equal(2, cs.Students.Count(s => s.DeletedAt == null));
    }

    [Fact]
    public void UnenrollStudent_Valid_Removes()
    {
        var cs = CourseSubject.Create(1, 1);
        cs.EnrollStudent(1);
        cs.UnenrollStudent(1);

        Assert.DoesNotContain(cs.Students, s => s.DeletedAt == null);
    }

    [Fact]
    public void UnenrollStudent_NonExistent_Throws()
    {
        var cs = CourseSubject.Create(1, 1);

        var ex = Assert.Throws<InvalidOperationException>(() => cs.UnenrollStudent(99));
        Assert.Contains("not enrolled", ex.Message.ToLower());
    }
}
