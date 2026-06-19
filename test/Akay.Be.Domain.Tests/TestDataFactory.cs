using System.Reflection;
using Akay.Be.Domain.Aggregates.Academic;
using Akay.Be.Domain.Aggregates.Identity;
using Akay.Be.Domain.Aggregates.Organization;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Tests;

internal static class TestDataFactory
{
    public static User CreateUser(string externalId = "ext-1", string email = "user@example.com", string displayName = "Test User")
    {
        return User.Create(externalId, email, displayName);
    }

    public static Organization CreateRootOrganization(Guid tenantId, string name = "Root Org")
    {
        return Organization.Create(tenantId, name, isCenter: false);
    }

    public static Organization CreateCenter(Guid tenantId, string name = "Center 1")
    {
        return Organization.Create(tenantId, name, isCenter: true);
    }

    public static AcademicPeriod CreateAcademicPeriod(Organization center, string name = "Period 2026")
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 12, 31);
        return AcademicPeriod.Create(center, name, start, end);
    }

    public static Subject CreateSubject(string code = "MATH", string name = "Math", Organization? organization = null)
    {
        return Subject.Create(code, name, organization);
    }

    public static void SetId<T>(T entity, int id) where T : Entity<int>
    {
        ArgumentNullException.ThrowIfNull(entity);
        var prop = typeof(T).GetProperty(nameof(Entity<int>.Id), BindingFlags.Public | BindingFlags.Instance)
                   ?? typeof(Entity<int>).GetProperty(nameof(Entity<int>.Id), BindingFlags.Public | BindingFlags.Instance);
        prop!.SetValue(entity, id);
    }
}
