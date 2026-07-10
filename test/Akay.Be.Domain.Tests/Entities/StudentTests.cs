using Akay.Be.Domain.Entities.Academic;

namespace Akay.Be.Domain.Tests.Entities;

public class StudentTests
{
    [Fact]
    public void Create_ZeroUserId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => Student.Create(0, 1));
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public void Create_ZeroCenterId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => Student.Create(1, 0));
        Assert.Contains("CenterId", ex.Message);
    }

    [Fact]
    public void Create_Valid_SetsProperties()
    {
        var student = Student.Create(1, 2, "STU001");

        Assert.Equal(1, student.UserId);
        Assert.Equal(2, student.CenterId);
        Assert.Equal("STU001", student.StudentNumber);
        Assert.True(student.IsActive);
    }

    [Fact]
    public void Activate_SetsActive()
    {
        var student = Student.Create(1, 2);
        student.Deactivate();
        student.Activate();

        Assert.True(student.IsActive);
    }

    [Fact]
    public void Deactivate_SetsInactive()
    {
        var student = Student.Create(1, 2);
        student.Deactivate();

        Assert.False(student.IsActive);
    }

    [Fact]
    public void ChangeStudentNumber_Updates()
    {
        var student = Student.Create(1, 2, "OLD");
        student.ChangeStudentNumber("NEW");

        Assert.Equal("NEW", student.StudentNumber);
    }

    [Fact]
    public void ChangeStudentNumber_ToNull_Clears()
    {
        var student = Student.Create(1, 2, "STU001");
        student.ChangeStudentNumber(null);

        Assert.Null(student.StudentNumber);
    }
}
