using Akay.Be.Domain.Aggregates.Identity;
using Akay.Be.Domain.Aggregates.Organization;

namespace Akay.Be.Domain.Tests;

public class UserRoleAssignmentTests
{
    [Fact]
    public void CreateSuperAdminWithOrganizationThrows()
    {
        var user = TestDataFactory.CreateUser();
        var role = TestDataFactory.CreateRole(RoleCodes.SuperAdmin, "Super");
        var org = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            UserRoleAssignment.Create(user, role, org));
    }

    [Fact]
    public void CreateSuperAdminWithoutOrganizationSucceeds()
    {
        var user = TestDataFactory.CreateUser();
        var role = TestDataFactory.CreateRole(RoleCodes.SuperAdmin, "Super");

        var assignment = UserRoleAssignment.Create(user, role, organization: null);

        Assert.Null(assignment.OrganizationId);
        Assert.Equal(user.Id, assignment.UserId);
        Assert.Equal(role.Id, assignment.RoleId);
    }

    [Fact]
    public void CreateOrganizationAdminOnCenterThrows()
    {
        var user = TestDataFactory.CreateUser();
        var role = TestDataFactory.CreateRole(RoleCodes.OrganizationAdmin, "Org Admin");
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            UserRoleAssignment.Create(user, role, center));
    }

    [Fact]
    public void CreateOrganizationAdminOnRootSucceeds()
    {
        var user = TestDataFactory.CreateUser();
        var role = TestDataFactory.CreateRole(RoleCodes.OrganizationAdmin, "Org Admin");
        var root = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        var assignment = UserRoleAssignment.Create(user, role, root);

        Assert.Equal(root.Id, assignment.OrganizationId);
    }

    [Fact]
    public void CreateCenterAdminOnRootThrows()
    {
        var user = TestDataFactory.CreateUser();
        var role = TestDataFactory.CreateRole(RoleCodes.CenterAdmin, "Center Admin");
        var root = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            UserRoleAssignment.Create(user, role, root));
    }

    [Fact]
    public void CreateCenterAdminOnCenterSucceeds()
    {
        var user = TestDataFactory.CreateUser();
        var role = TestDataFactory.CreateRole(RoleCodes.CenterAdmin, "Center Admin");
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());

        var assignment = UserRoleAssignment.Create(user, role, center);

        Assert.Equal(center.Id, assignment.OrganizationId);
    }

    [Fact]
    public void CreateTeacherWithoutOrganizationThrows()
    {
        var user = TestDataFactory.CreateUser();
        var role = TestDataFactory.CreateRole(RoleCodes.Teacher, "Teacher");

        Assert.Throws<InvalidOperationException>(() =>
            UserRoleAssignment.Create(user, role, organization: null));
    }

    [Fact]
    public void CreateTeacherOnRootSucceeds()
    {
        var user = TestDataFactory.CreateUser();
        var role = TestDataFactory.CreateRole(RoleCodes.Teacher, "Teacher");
        var root = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        var assignment = UserRoleAssignment.Create(user, role, root);

        Assert.Equal(root.Id, assignment.OrganizationId);
    }

    [Fact]
    public void CreateTeacherOnCenterSucceeds()
    {
        var user = TestDataFactory.CreateUser();
        var role = TestDataFactory.CreateRole(RoleCodes.Teacher, "Teacher");
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());

        var assignment = UserRoleAssignment.Create(user, role, center);

        Assert.Equal(center.Id, assignment.OrganizationId);
    }

    [Fact]
    public void SameUserCanHaveMultipleRolesOnSameCenter()
    {
        var user = TestDataFactory.CreateUser();
        var centerAdmin = TestDataFactory.CreateRole(RoleCodes.CenterAdmin, "Center Admin");
        var teacher = TestDataFactory.CreateRole(RoleCodes.Teacher, "Teacher");
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());

        var first = UserRoleAssignment.Create(user, centerAdmin, center);
        var second = UserRoleAssignment.Create(user, teacher, center);

        Assert.Equal(user.Id, first.UserId);
        Assert.Equal(user.Id, second.UserId);
        Assert.Equal(center.Id, first.OrganizationId);
        Assert.Equal(center.Id, second.OrganizationId);
        Assert.NotSame(first.Role, second.Role);
        Assert.Same(centerAdmin, first.Role);
        Assert.Same(teacher, second.Role);
    }
}
