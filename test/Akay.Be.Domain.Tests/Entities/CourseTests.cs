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

        Assert.Equal(2, course.Subjects.Count(s => s.DeletedAt == null));
    }

    [Fact]
    public void RemoveSubject_Valid_Removes()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.AddSubject(1);
        course.RemoveSubject(1);

        Assert.DoesNotContain(course.Subjects, s => s.DeletedAt == null);
    }

    [Fact]
    public void RemoveSubject_NonExistent_Throws()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");

        var ex = Assert.Throws<InvalidOperationException>(() => course.RemoveSubject(99));
        Assert.Contains("not assigned", ex.Message.ToLower());
    }

    [Fact]
    public void EnrollStudent_Duplicate_Throws()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.EnrollStudent(1);

        var ex = Assert.Throws<InvalidOperationException>(() => course.EnrollStudent(1));
        Assert.Contains("already", ex.Message.ToLower());
    }

    [Fact]
    public void EnrollStudent_Valid_Adds()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.EnrollStudent(1);
        course.EnrollStudent(2);

        Assert.Equal(2, course.Students.Count(s => s.DeletedAt == null));
    }

    [Fact]
    public void UnenrollStudent_Valid_Removes()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");
        course.EnrollStudent(1);
        course.UnenrollStudent(1);

        Assert.DoesNotContain(course.Students, s => s.DeletedAt == null);
    }

    [Fact]
    public void UnenrollStudent_NonExistent_Throws()
    {
        var course = Course.Create(1, "1º ESO", "ESO1");

        var ex = Assert.Throws<InvalidOperationException>(() => course.UnenrollStudent(99));
        Assert.Contains("not enrolled", ex.Message.ToLower());
    }
}
