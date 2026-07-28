using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.Be.Domain.Events.Identity;

namespace Akay.Be.Domain.Tests.Entities;

public class UserTests
{
    [Fact]
    public void AssignGlobalRole_SuperAdmin_WithoutCenter_Succeeds()
    {
        var user = User.Create("test@example.com", "Test", "User");
        user.AssignGlobalRole(UserRole.SuperAdmin);

        Assert.Contains(user.RoleAssignments, r => r.Role == UserRole.SuperAdmin && r.CenterId == null);
    }

    [Fact]
    public void AssignGlobalRole_SuperAdmin_WithCenter_Throws()
    {
        var user = User.Create("test@example.com", "Test", "User");

        var ex = Assert.Throws<InvalidOperationException>(() => user.AssignRole(1, UserRole.SuperAdmin));
        Assert.Contains("SuperAdmin", ex.Message);
    }

    [Fact]
    public void AssignRole_Admin_WithoutCenter_Throws()
    {
        var user = User.Create("test@example.com", "Test", "User");

        var ex = Assert.Throws<InvalidOperationException>(() => user.AssignGlobalRole(UserRole.Admin));
        Assert.Contains("center", ex.Message.ToLower());
    }

    [Fact]
    public void AssignRole_Teacher_WithoutCenter_Throws()
    {
        var user = User.Create("test@example.com", "Test", "User");

        var ex = Assert.Throws<InvalidOperationException>(() => user.AssignGlobalRole(UserRole.Teacher));
        Assert.Contains("center", ex.Message.ToLower());
    }

    [Fact]
    public void AssignRole_Student_WithoutCenter_Throws()
    {
        var user = User.Create("test@example.com", "Test", "User");

        var ex = Assert.Throws<InvalidOperationException>(() => user.AssignGlobalRole(UserRole.Student));
        Assert.Contains("center", ex.Message.ToLower());
    }

    [Fact]
    public void AssignRole_DuplicateAssignment_Throws()
    {
        var user = User.Create("test@example.com", "Test", "User");
        user.AssignRole(1, UserRole.Teacher);

        var ex = Assert.Throws<InvalidOperationException>(() => user.AssignRole(1, UserRole.Teacher));
        Assert.Contains("already", ex.Message.ToLower());
    }

    [Fact]
    public void AssignRole_MultipleRoles_SameCenter_Succeeds()
    {
        var user = User.Create("test@example.com", "Test", "User");
        user.AssignRole(1, UserRole.Teacher);
        user.AssignRole(1, UserRole.Student);

        Assert.Equal(2, user.RoleAssignments.Count(r => r.DeletedAt == null));
    }

    [Fact]
    public void AssignRole_SameRole_DifferentCenters_Succeeds()
    {
        var user = User.Create("test@example.com", "Test", "User");
        user.AssignRole(1, UserRole.Teacher);
        user.AssignRole(2, UserRole.Teacher);

        Assert.Equal(2, user.RoleAssignments.Count(r => r.DeletedAt == null));
    }

    [Fact]
    public void AssignGlobalRole_DuplicateSuperAdmin_Throws()
    {
        var user = User.Create("test@example.com", "Test", "User");
        user.AssignGlobalRole(UserRole.SuperAdmin);

        var ex = Assert.Throws<InvalidOperationException>(() => user.AssignGlobalRole(UserRole.SuperAdmin));
        Assert.Contains("already", ex.Message.ToLower());
    }

    [Fact]
    public void RemoveRole_RemovesAssignment()
    {
        var user = User.Create("test@example.com", "Test", "User");
        user.AssignRole(1, UserRole.Teacher);

        user.RemoveRole(1, UserRole.Teacher);

        Assert.DoesNotContain(user.RoleAssignments, r => r.CenterId == 1 && r.Role == UserRole.Teacher && r.DeletedAt == null);
    }

    [Fact]
    public void RemoveRole_NonExistent_Throws()
    {
        var user = User.Create("test@example.com", "Test", "User");

        var ex = Assert.Throws<InvalidOperationException>(() => user.RemoveRole(1, UserRole.Teacher));
        Assert.Contains("does not have", ex.Message.ToLower());
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var user = User.Create("test@example.com", "Test", "User");
        user.Deactivate();
        user.Activate();

        Assert.True(user.IsActive);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = User.Create("test@example.com", "Test", "User");
        user.Deactivate();

        Assert.False(user.IsActive);
    }

    [Fact]
    public void SetExternalId_SetsValue()
    {
        var user = User.Create("test@example.com", "Test", "User");
        var id = Guid.NewGuid();

        user.SetExternalId(id);

        Assert.Equal(id, user.ExternalId);
    }

    [Fact]
    public void SetExternalId_Empty_Throws()
    {
        var user = User.Create("test@example.com", "Test", "User");

        var ex = Assert.Throws<ArgumentException>(() => user.SetExternalId(Guid.Empty));
        Assert.Contains("empty", ex.Message.ToLower());
    }

    [Fact]
    public void UpdateProfile_Should_Clear_ExternalId_And_Raise_Outbox_Event_When_Email_Changes()
    {
        var user = User.Create("old@example.com", "Test", "User");
        var externalId = Guid.NewGuid();
        user.SetExternalId(externalId);

        user.UpdateProfile("new@example.com", "New", "User");

        Assert.Null(user.ExternalId);
        var cleanupEvent = Assert.Single(user.AfterSaveDomainEvents.OfType<ExternalIdentityCleanupRequestedOutboxEvent>());
        Assert.Equal(externalId, cleanupEvent.ExternalId);
        Assert.Equal("old@example.com", cleanupEvent.Email);
        Assert.Equal(ExternalIdentityCleanupReasons.EmailChanged, cleanupEvent.Reason);
    }

    [Fact]
    public void SoftDelete_Should_Raise_Outbox_Event_When_ExternalId_Exists()
    {
        var user = User.Create("test@example.com", "Test", "User");
        var externalId = Guid.NewGuid();
        user.SetExternalId(externalId);

        user.SoftDelete();

        Assert.NotNull(user.DeletedAt);
        var cleanupEvent = Assert.Single(user.AfterSaveDomainEvents.OfType<ExternalIdentityCleanupRequestedOutboxEvent>());
        Assert.Equal(externalId, cleanupEvent.ExternalId);
        Assert.Equal("test@example.com", cleanupEvent.Email);
        Assert.Equal(ExternalIdentityCleanupReasons.LocalUserDeleted, cleanupEvent.Reason);
    }
}
