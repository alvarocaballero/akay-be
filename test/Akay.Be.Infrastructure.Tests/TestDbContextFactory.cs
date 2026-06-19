using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.EF.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Tests;

internal static class TestDbContextFactory
{
    public static ApplicationDbContext CreateContext()
    {
        const string connection = "Server=localhost;Database=AkayBeTests;Integrated Security=true;TrustServerCertificate=true;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connection);

        var userContext = new TestUserContext();
        var registration = new DbContextRegistration<ApplicationDbContext>(
            new DbContextSettings(),
            (_, _, _) => { });

        return new ApplicationDbContext(userContext, registration, optionsBuilder.Options);
    }
}

internal sealed class TestUserContext : IUserContext
{
    public bool IsAuthenticated => false;
    public int UserId => 0;
    public string Name => string.Empty;
    public string Email => string.Empty;
    public IEnumerable<string> Roles => Array.Empty<string>();
    public bool IsApiKey => false;
    public bool IsBearer => false;
    public bool IsMasterApiKey => false;
    public Guid? TenantId => null;
}
