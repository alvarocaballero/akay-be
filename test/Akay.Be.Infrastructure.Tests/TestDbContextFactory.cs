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
        const string connection = "Host=localhost;Database=AkayBeTests;Username=postgres;Password=postgres123;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connection);

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
}
