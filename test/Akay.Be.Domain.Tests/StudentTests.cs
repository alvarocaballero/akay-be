using Akay.Be.Domain.Aggregates.Academic;

namespace Akay.Be.Domain.Tests;

public class StudentTests
{
    [Fact]
    public void CreateValidStudentSetsProperties()
    {
        var center = TestDataFactory.CreateCenter(Guid.NewGuid());

        var student = Student.Create(userId: 10, center);

        Assert.Equal(10, student.UserId);
        Assert.Equal(center.Id, student.CenterId);
        Assert.True(student.IsActive);
    }

    [Fact]
    public void CreateOnRootOrganizationThrows()
    {
        var root = TestDataFactory.CreateRootOrganization(Guid.NewGuid());

        Assert.Throws<ArgumentException>(() =>
            Student.Create(userId: 10, root));
    }
}
