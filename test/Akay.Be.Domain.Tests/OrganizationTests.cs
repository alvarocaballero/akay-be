using Akay.Be.Domain.Aggregates.Organization;

namespace Akay.Be.Domain.Tests;

public class OrganizationTests
{
    [Fact]
    public void CreateRootOrganizationSetsIsCenterFalse()
    {
        var tenantId = Guid.NewGuid();

        var organization = Organization.Create(tenantId, "Root", isCenter: false);

        Assert.Equal(tenantId, organization.TenantId);
        Assert.Equal("Root", organization.Name);
        Assert.False(organization.IsCenter);
        Assert.True(organization.IsActive);
    }

    [Fact]
    public void CreateCenterSetsIsCenterTrue()
    {
        var tenantId = Guid.NewGuid();

        var organization = Organization.Create(tenantId, "Center A", isCenter: true);

        Assert.True(organization.IsCenter);
    }

    [Fact]
    public void CreateEmptyNameThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            Organization.Create(Guid.NewGuid(), string.Empty, isCenter: false));
    }

    [Fact]
    public void CreateEmptyTenantIdThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            Organization.Create(Guid.Empty, "Org", isCenter: false));
    }

    [Fact]
    public void RenameUpdatesName()
    {
        var org = Organization.Create(Guid.NewGuid(), "Old", isCenter: false);

        org.Rename("New");

        Assert.Equal("New", org.Name);
    }
}
