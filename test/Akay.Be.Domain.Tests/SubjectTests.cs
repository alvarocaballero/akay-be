using Akay.Be.Domain.Aggregates.Academic;
using Akay.Be.Domain.Aggregates.Organization;

namespace Akay.Be.Domain.Tests;

public class SubjectTests
{
    [Fact]
    public void CreateValidSubjectSetsProperties()
    {
        var subject = Subject.Create("MATH", "Mathematics");

        Assert.Equal("MATH", subject.Code);
        Assert.Equal("Mathematics", subject.Name);
        Assert.Null(subject.Version);
        Assert.Null(subject.OrganizationId);
        Assert.True(subject.IsActive);
    }

    [Fact]
    public void CreateWithOrganizationSetsOrganizationId()
    {
        var org = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        var subject = Subject.Create("MATH", "Mathematics", org);

        Assert.Equal(org.Id, subject.OrganizationId);
    }

    [Fact]
    public void CreateWithVersionSetsVersion()
    {
        var subject = Subject.Create("MATH", "Math", null, "v2");

        Assert.Equal("v2", subject.Version);
    }

    [Fact]
    public void CreateEmptyCodeThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            Subject.Create(string.Empty, "Mathematics"));
    }

    [Fact]
    public void CreateEmptyNameThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            Subject.Create("MATH", string.Empty));
    }

    [Fact]
    public void RenameUpdatesName()
    {
        var subject = Subject.Create("MATH", "Old");

        subject.Rename("New");

        Assert.Equal("New", subject.Name);
    }

    [Fact]
    public void AssignToOrganizationSetsOrganizationId()
    {
        var subject = Subject.Create("MATH", "Math");
        var org = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        subject.AssignToOrganization(org);

        Assert.Equal(org.Id, subject.OrganizationId);
    }

    [Fact]
    public void AssignToNullClearsOrganization()
    {
        var org = TestDataFactory.CreateRootOrganization(Guid.NewGuid());
        var subject = Subject.Create("MATH", "Math", org);

        subject.AssignToOrganization(null);

        Assert.Null(subject.OrganizationId);
    }

    [Fact]
    public void DeactivateSetsIsActiveFalse()
    {
        var subject = Subject.Create("MATH", "Math");

        subject.Deactivate();

        Assert.False(subject.IsActive);
    }

    [Fact]
    public void ActivateSetsIsActiveTrue()
    {
        var subject = Subject.Create("MATH", "Math");
        subject.Deactivate();

        subject.Activate();

        Assert.True(subject.IsActive);
    }
}
