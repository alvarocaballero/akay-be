using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Organization;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.EF.Domain.Outbox;
using Akay.To.EF.Infrastructure.DbContexts;
using Akay.To.EF.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Tests.Persistence;

public sealed class AcademicPeriodOutboxIntegrationTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .AddInterceptors(new DomainEventsSaveChangesInterceptor(TimeProvider.System))
            .Options;

        var userContext = new TestUserContext();
        var registration = new DbContextRegistration<ApplicationDbContext>(
            new DbContextSettings(),
            (_, _, _) => { });

        return new ApplicationDbContext(userContext, registration, options);
    }

    private sealed class TestUserContext : IUserContext
    {
        public bool IsAuthenticated => false;
        public int UserId => 0;
        public string Name => string.Empty;
        public string Email => string.Empty;
        public IEnumerable<string> Roles => Array.Empty<string>();
    }

    [Fact]
    public async Task SaveChanges_CreateAcademicPeriod_Should_Write_OutboxMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = CreateContext(Guid.NewGuid().ToString());

        var center = Center.Create("Center", "CTR");
        context.Centers.Add(center);
        await context.SaveChangesAsync(cancellationToken);

        var period = AcademicPeriod.Create(center.Id, "2026-2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 6, 30));
        context.AcademicPeriods.Add(period);
        await context.SaveChangesAsync(cancellationToken);

        var messages = await context.Set<OutboxMessage>()
            .ToListAsync(cancellationToken);

        var message = Assert.Single(messages);
        Assert.Contains("AcademicPeriodCreatedOutboxEvent", message.Type);
        Assert.Contains(period.SyncId.ToString(), message.Content);
        Assert.Empty(period.AfterSaveDomainEvents);
    }
}
