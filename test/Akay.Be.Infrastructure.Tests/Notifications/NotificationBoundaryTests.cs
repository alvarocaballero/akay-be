using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Akay.Be.Application;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Notifications;
using Akay.Be.Application.Services;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Entities.Notification;
using Akay.Be.Infrastructure;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Services;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.ApplicationSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Infrastructure.Tests.Notifications;

public sealed class NotificationBoundaryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ProductionRegistration_CommitsBeforePublication_AndRejectionsPreserveStoredData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var seed = await postgres.CreateServicesAsync();
        await using var seedScope = seed.CreateAsyncScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = new ApplicationSettings { AzureSignalRSettings = new() };
        settings.DbContextSettings["ApplicationDbContext"] = new DbContextSettings
        {
            ConnectionString = seedDb.Database.GetConnectionString(),
            Interceptors = new() { TenantAndOwnership = false }
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(seedScope.ServiceProvider.GetRequiredService<IUserContext>());
        services.AddInfrastructureServices(settings).AddApplicationServices(settings);
        await using (var registration = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }))
        {
            await using var registrationScope = registration.CreateAsyncScope();
            Assert.IsType<SignalRNotificationPublisher>(registrationScope.ServiceProvider.GetRequiredService<INotificationPublisher>());
            Assert.IsType<NotificationService>(registrationScope.ServiceProvider.GetRequiredService<INotificationService>());
        }

        services.AddSingleton<CommitObserver>();
        services.AddSingleton<INotificationPublisher>(provider => provider.GetRequiredService<CommitObserver>());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(User.Create("uncommitted@example.test", "Pending", "User"));
        await scope.ServiceProvider.GetRequiredService<INotificationService>()
                   .SendAsync(new Notice(12, "probe", "Title", "Message", null, 42), ct);
        var observer = provider.GetRequiredService<CommitObserver>();
        Assert.Equal(1, observer.Calls);
        Assert.True(observer.Committed);
        Assert.False(await seedDb.Users.AnyAsync(ct));
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var own = Assert.Single((await repository.GetPagedAsync(12, 1, 100, ct)).Data);
        using var payload = JsonDocument.Parse(own.Payload!);
        Assert.Equal(42, payload.RootElement.GetProperty("extra").GetInt32());
        var foreign = PersistentNotification.Create(99, "probe", "Foreign", "Message", null, null, DateTimeOffset.UtcNow);
        await repository.PersistAsync(foreign, ct);

        foreach (var id in new[] { foreign.Id, int.MaxValue, 0 })
        {
            var before = await HashAsync(db, ct);
            Assert.False(await repository.MarkReadAsync(12, id, DateTimeOffset.UtcNow, ct));
            Assert.Equal(before, await HashAsync(db, ct));
            Assert.False(await repository.DeleteForUserAsync(12, id, ct));
            Assert.Equal(before, await HashAsync(db, ct));
        }

        var snapshot = await HashAsync(db, ct);
        var malformed = PersistentNotification.Create(12, "probe", "Bad JSON", "Message", null, "{", DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<DbUpdateException>(() => repository.PersistAsync(malformed, ct));
        Assert.Equal(snapshot, await HashAsync(db, ct));
        var first = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.True(await repository.MarkReadAsync(12, own.Id, first, ct));
        await repository.MarkAllReadAsync(12, first.AddDays(1), ct);
        await repository.MarkAllReadAsync(12, first.AddDays(2), ct);
        var ownReadAt = await db.Notifications.AsNoTracking().Where(n => n.Id == own.Id).Select(n => n.ReadAt).SingleAsync(ct);
        Assert.Equal(first, ownReadAt);
        Assert.Equal(0, await repository.GetUnreadCountAsync(12, ct));
        Assert.Equal(1, await repository.GetUnreadCountAsync(99, ct));
        Assert.True(await repository.DeleteForUserAsync(12, own.Id, ct));
        Assert.Equal(foreign.Id, await db.Notifications.IgnoreQueryFilters().AsNoTracking().Select(n => n.Id).SingleAsync(ct));
    }

    private static async Task<string> HashAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        const string query = "SELECT row_to_json(n)::text AS \"Value\" FROM notification.notification n ORDER BY id";
        var rows = await db.Database.SqlQueryRaw<string>(query).ToListAsync(cancellationToken);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows))));
    }

    private sealed record Notice(int UserId, string Type, string Title, string Message, string? Url, int Extra) : IPersistentNotification;

    private sealed class CommitObserver(IServiceScopeFactory scopes) : INotificationPublisher
    {
        public int Calls { get; private set; }
        public bool Committed { get; private set; }

        public async Task PublishAsync(string userId, NotificationMessage message, CancellationToken cancellationToken)
        {
            Calls++;
            var notificationId = message.NotificationId!.Value;
            var parsedUserId = int.Parse(userId, System.Globalization.CultureInfo.InvariantCulture);
            await using var scope = scopes.CreateAsyncScope();
            Committed = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications
                                   .AnyAsync(n => n.Id == notificationId && n.UserId == parsedUserId, cancellationToken);
            throw new InvalidOperationException("Independent publication failure");
        }
    }
}
