using Akay.Be.Domain.Aggregates.Identity;
using Akay.Be.Domain.Enums;
using Akay.Be.Domain.Aggregates.Organization;

namespace Akay.Be.Domain.Tests;

public class UserRoleAssignmentTests
{
    [Fact]
    public void CreateSuperAdminWithOrganizationThrows()
    {
        var user = TestDataFactory.CreateUser();
        var org = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            UserRoleAssignment.Create(user, UserRole.SuperAdmin, org));
    }

    [Fact]
    public void CreateSuperAdminWithoutOrganizationSucceeds()
    {
        var user = TestDataFactory.CreateUser();

        var assignment = UserRoleAssignment.Create(user, UserRole.SuperAdmin, organization: null);

        Assert.Null(assignment.OrganizationId);
        Assert.Equal(user.Id, assignment.UserId);
        Assert.Equal((int)UserRole.SuperAdmin, assignment.RoleId);
    }

    [Fact]
    public void CreateAdminOnRootSucceeds()
    {
        var user = TestDataFactory.CreateUser();
        var root = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        var assignment = UserRoleAssignment.Create(user, UserRole.Admin, root);

        Assert.Equal(root.Id, assignment.OrganizationId);
        Assert.Equal((int)UserRole.Admin, assignment.RoleId);
    }

    [Fact]
    public void CreateAdminOnCenterSucceeds()
    {
        var user = TestDataFactory.CreateUser();
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());

        var assignment = UserRoleAssignment.Create(user, UserRole.Admin, center);

        Assert.Equal(center.Id, assignment.OrganizationId);
    }

    [Fact]
    public void CreateTeacherWithoutOrganizationThrows()
    {
        var user = TestDataFactory.CreateUser();

        Assert.Throws<InvalidOperationException>(() =>
            UserRoleAssignment.Create(user, UserRole.Teacher, organization: null));
    }

    [Fact]
    public void CreateTeacherOnRootSucceeds()
    {
        var user = TestDataFactory.CreateUser();
        var root = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        var assignment = UserRoleAssignment.Create(user, UserRole.Teacher, root);

        Assert.Equal(root.Id, assignment.OrganizationId);
    }

    [Fact]
    public void CreateTeacherOnCenterSucceeds()
    {
        var user = TestDataFactory.CreateUser();
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());

        var assignment = UserRoleAssignment.Create(user, UserRole.Teacher, center);

        Assert.Equal(center.Id, assignment.OrganizationId);
    }

    [Fact]
    public void SameUserCanHaveMultipleRolesOnSameCenter()
    {
        var user = TestDataFactory.CreateUser();
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());

        var first = UserRoleAssignment.Create(user, UserRole.Admin, center);
        var second = UserRoleAssignment.Create(user, UserRole.Teacher, center);

        Assert.Equal(user.Id, first.UserId);
        Assert.Equal(user.Id, second.UserId);
        Assert.Equal(center.Id, first.OrganizationId);
        Assert.Equal(center.Id, second.OrganizationId);
        Assert.NotEqual(first.RoleId, second.RoleId);
        Assert.Equal((int)UserRole.Admin, first.RoleId);
        Assert.Equal((int)UserRole.Teacher, second.RoleId);
    }
}
