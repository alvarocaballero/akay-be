using Akay.Be.Domain.Aggregates.Identity;

namespace Akay.Be.Domain.Tests;

public class RoleTests
{
    [Fact]
    public void CreateValidRoleSetsProperties()
    {
        var role = Role.Create("Teacher", "Teacher");

        Assert.Equal("Teacher", role.Code);
        Assert.Equal("Teacher", role.Name);
    }

    [Fact]
    public void CreateEmptyCodeThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            Role.Create(string.Empty, "Teacher"));
    }

    [Fact]
    public void CreateEmptyNameThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            Role.Create("Teacher", string.Empty));
    }

    [Fact]
    public void CreateCodeExceedsMaxLength()
    {
        Assert.Throws<ArgumentException>(() =>
            Role.Create(new string('X', 51), "Name"));
    }
}
