using Akay.Be.Domain.Entities.Academic;

namespace Akay.Be.Domain.Tests.Entities;

public class SubjectTests
{
    [Fact]
    public void Create_EmptyName_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Subject.Create(" ", null, [1]));
        Assert.Contains("name", ex.Message.ToLower());
    }

    [Fact]
    public void Create_NoCenters_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Subject.Create("Test", null, []));
        Assert.Contains("center", ex.Message.ToLower());
    }

    [Fact]
    public void Create_InvalidCenterId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Subject.Create("Test", null, [0]));
        Assert.Contains("center", ex.Message.ToLower());
    }

    [Fact]
    public void Create_Valid_SetsProperties()
    {
        var subject = Subject.Create("Math", "Description", [1, 2]);

        Assert.Equal("Math", subject.Name);
        Assert.Equal("Description", subject.Description);
        Assert.Equal(2, subject.Centers.Count);
    }

    [Fact]
    public void AddCenter_Duplicate_Throws()
    {
        var subject = Subject.Create("Math", null, [1]);

        var ex = Assert.Throws<InvalidOperationException>(() => subject.AddCenter(1));
        Assert.Contains("already", ex.Message.ToLower());
    }

    [Fact]
    public void AddCenter_Valid_Adds()
    {
        var subject = Subject.Create("Math", null, [1]);
        subject.AddCenter(2);

        Assert.Equal(2, subject.Centers.Count(c => c.DeletedAt == null));
    }

    [Fact]
    public void RemoveCenter_LastActiveCenter_Throws()
    {
        var subject = Subject.Create("Math", null, [1]);

        var ex = Assert.Throws<InvalidOperationException>(() => subject.RemoveCenter(1));
        Assert.Contains("last", ex.Message.ToLower());
    }

    [Fact]
    public void RemoveCenter_NonExistent_Throws()
    {
        var subject = Subject.Create("Math", null, [1, 2]);

        var ex = Assert.Throws<InvalidOperationException>(() => subject.RemoveCenter(99));
        Assert.Contains("not associated", ex.Message.ToLower());
    }

    [Fact]
    public void RemoveCenter_Valid_Removes()
    {
        var subject = Subject.Create("Math", null, [1, 2]);
        subject.RemoveCenter(1);

        Assert.Single(subject.Centers, c => c.DeletedAt == null);
    }

    [Fact]
    public void AddAdmin_Duplicate_Throws()
    {
        var subject = Subject.Create("Math", null, [1]);
        subject.AddAdmin(1);

        var ex = Assert.Throws<InvalidOperationException>(() => subject.AddAdmin(1));
        Assert.Contains("already", ex.Message.ToLower());
    }

    [Fact]
    public void AddAdmin_Valid_Adds()
    {
        var subject = Subject.Create("Math", null, [1]);
        subject.AddAdmin(1);
        subject.AddAdmin(2);

        Assert.Equal(2, subject.Admins.Count(a => a.DeletedAt == null));
    }

    [Fact]
    public void RemoveAdmin_Valid_Removes()
    {
        var subject = Subject.Create("Math", null, [1]);
        subject.AddAdmin(1);
        subject.RemoveAdmin(1);

        Assert.DoesNotContain(subject.Admins, a => a.DeletedAt == null);
    }

    [Fact]
    public void RemoveAdmin_NonExistent_Throws()
    {
        var subject = Subject.Create("Math", null, [1]);

        var ex = Assert.Throws<InvalidOperationException>(() => subject.RemoveAdmin(99));
        Assert.Contains("not an admin", ex.Message.ToLower());
    }

    [Fact]
    public void ChangeName_Updates()
    {
        var subject = Subject.Create("Math", null, [1]);
        subject.ChangeName("Science");

        Assert.Equal("Science", subject.Name);
    }

    [Fact]
    public void ChangeName_Empty_Throws()
    {
        var subject = Subject.Create("Math", null, [1]);

        var ex = Assert.Throws<ArgumentException>(() => subject.ChangeName(" "));
        Assert.Contains("name", ex.Message.ToLower());
    }

    [Fact]
    public void ChangeDescription_Updates()
    {
        var subject = Subject.Create("Math", "Old", [1]);
        subject.ChangeDescription("New");

        Assert.Equal("New", subject.Description);
    }

    [Fact]
    public void ChangeDescription_ToNull_Clears()
    {
        var subject = Subject.Create("Math", "Old", [1]);
        subject.ChangeDescription(null);

        Assert.Null(subject.Description);
    }
}
