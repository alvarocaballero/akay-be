using Akay.Be.Domain.Aggregates.Identity;

namespace Akay.Be.Domain.Tests;

public class UserTests
{
    [Fact]
    public void CreateValidUserSetsProperties()
    {
        var user = User.Create("ext-abc-123", "user@example.com", "Test User");

        Assert.Equal("ext-abc-123", user.ExternalId);
        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("Test User", user.DisplayName);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void CreateEmptyExternalIdThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create(string.Empty, "user@example.com", "Name"));
    }

    [Fact]
    public void CreateEmptyEmailThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create("ext-1", string.Empty, "Name"));
    }

    [Fact]
    public void CreateEmptyDisplayNameThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create("ext-1", "user@example.com", string.Empty));
    }

    [Fact]
    public void UpdateDisplayNameChangesName()
    {
        var user = User.Create("ext-1", "user@example.com", "Old Name");

        user.UpdateDisplayName("New Name");

        Assert.Equal("New Name", user.DisplayName);
    }

    [Fact]
    public void DeactivateSetsIsActiveFalse()
    {
        var user = User.Create("ext-1", "user@example.com", "Name");

        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void ActivateSetsIsActiveTrue()
    {
        var user = User.Create("ext-1", "user@example.com", "Name");
        user.Deactivate();

        user.Activate();

        Assert.True(user.IsActive);
    }
}
