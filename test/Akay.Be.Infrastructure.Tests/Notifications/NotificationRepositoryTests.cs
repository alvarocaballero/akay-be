using System.Text.Json;
using System.Transactions;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Notifications;
using Akay.Be.Application.Services;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Entities.Notification;
using Akay.Be.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akay.Be.Infrastructure.Tests.Notifications;

public sealed class NotificationRepositoryTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static PersistentNotification Create(int userId, int age = 0, string? url = null) =>
        PersistentNotification.Create(userId, "completed", "Title", "Message", url, "{\"topicId\":42}", DateTimeOffset.UtcNow.AddMinutes(-age));

    [Fact]
    public async Task Persist_UsesJsonbAndExpectedIndexes_WithoutSavingBusinessChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var services = await postgres.CreateServicesAsync();
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Users.Add(User.Create("pending@example.test", "Pending", "Business"));
        var record = Create(12, url: "/topics/42");

        await scope.ServiceProvider.GetRequiredService<INotificationRepository>().PersistAsync(record, ct);

        await using var readScope = services.CreateAsyncScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await readContext.Notifications.SingleAsync(ct);
        Assert.Equal(record.Id, stored.Id);
        Assert.Equal("/topics/42", stored.Url);
        Assert.Null(stored.ReadAt);
        Assert.Equal(42, JsonDocument.Parse(stored.Payload!).RootElement.GetProperty("topicId").GetInt32());
        Assert.Equal(0, await readContext.Users.CountAsync(ct));
        Assert.Equal(EntityState.Added, context.Entry(context.Users.Local.Single()).State);
        var model = readContext.Model.FindEntityType(typeof(PersistentNotification))!;
        Assert.Equal("jsonb", model.FindProperty(nameof(PersistentNotification.Payload))!.GetColumnType());
        Assert.Equal("notification", model.GetSchema());
        Assert.Equal("notification", model.GetTableName());
        Assert.Equal("PK_notifications", model.FindPrimaryKey()!.GetName());
        Assert.Equal(2, model.GetIndexes().Count());
        Assert.Contains(model.GetIndexes(), index => index.GetFilter() == "read_at IS NULL");
        const string typeQuery = "SELECT pg_typeof(payload)::text AS \"Value\" FROM notification.notification";
        var payloadType = await readContext.Database.SqlQueryRaw<string>(typeQuery).SingleAsync(ct);
        Assert.Equal("jsonb", payloadType);
    }

    [Fact]
    public async Task List_ReturnsUnreadDescendingAndOwnerFiltered()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var services = await postgres.CreateServicesAsync();
        await using var scope = services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var newest = Create(12);
        var older = Create(12, 1);
        var read = Create(12, 2);
        read.MarkRead(DateTimeOffset.UtcNow);
        foreach (var notification in new[] { newest, older, read, Create(99) })
            await repository.PersistAsync(notification, ct);

        var first = await repository.GetPagedAsync(12, 1, 2, ct);
        var second = await repository.GetPagedAsync(12, 2, 2, ct);
        Assert.Equal(new[] { newest.Id, older.Id }, first.Data.Select(n => n.Id));
        Assert.False(first.HasMoreItems);
        Assert.Empty(second.Data);
        Assert.Equal(2, await repository.GetUnreadCountAsync(12, ct));
        Assert.Equal(1, await repository.GetUnreadCountAsync(99, ct));
    }

    [Fact]
    public async Task ReadAndDelete_AreOwnerScoped_IdempotentAndPhysical()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var services = await postgres.CreateServicesAsync();
        await using var scope = services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var own = Create(12);
        var foreign = Create(99);
        var readAt = DateTimeOffset.UtcNow.AddMinutes(1);
        await repository.PersistAsync(own, ct);
        await repository.PersistAsync(foreign, ct);

        Assert.False(await repository.MarkReadAsync(12, foreign.Id, readAt, ct));
        Assert.False(await repository.MarkReadAsync(12, int.MaxValue, readAt, ct));
        Assert.True(await repository.MarkReadAsync(12, own.Id, readAt, ct));
        Assert.True(await repository.MarkReadAsync(12, own.Id, readAt.AddMinutes(1), ct));
        await repository.MarkAllReadAsync(12, readAt.AddMinutes(2), ct);
        var ownReadAt = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications
            .AsNoTracking().Where(n => n.Id == own.Id).Select(n => n.ReadAt).SingleAsync(ct);
        Assert.Equal(readAt.ToUnixTimeMilliseconds(), ownReadAt!.Value.ToUnixTimeMilliseconds());
        Assert.Equal(0, await repository.GetUnreadCountAsync(12, ct));
        Assert.Equal(1, await repository.GetUnreadCountAsync(99, ct));
        Assert.False(await repository.DeleteForUserAsync(12, foreign.Id, ct));
        Assert.True(await repository.DeleteForUserAsync(12, own.Id, ct));
        Assert.False(await repository.DeleteForUserAsync(12, own.Id, ct));
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Notifications
                                .IgnoreQueryFilters().AnyAsync(n => n.Id == own.Id, ct));
        Assert.Equal(foreign.Id, Assert.Single((await repository.GetPagedAsync(99, 1, 10, ct)).Data).Id);
    }

    [Fact]
    public async Task SignalRFailure_LeavesCommittedNotificationAvailableToQueries()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var services = await postgres.CreateServicesAsync();
        await using var scope = services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var service = new NotificationService(repository, new FailingPublisher(), NullLogger<NotificationService>.Instance);

        await service.SendAsync(new TestNotification(12, "complete", "Done", "Finished", null), ct);

        var stored = Assert.Single((await repository.GetPagedAsync(12, 1, 10, ct)).Data);
        Assert.Equal("Done", stored.Title);
        Assert.Null(stored.Url);
    }

    [Fact]
    public async Task AmbientTransaction_IsRejectedWithoutStoringAnything()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var services = await postgres.CreateServicesAsync();
        await using var scope = services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        using (var transaction = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.PersistAsync(Create(12), ct));
        Assert.Empty((await repository.GetPagedAsync(12, 1, 10, ct)).Data);
    }

    private sealed record TestNotification(int UserId, string Type, string Title, string Message, string? Url) : IPersistentNotification;
    private sealed class FailingPublisher : INotificationPublisher
    {
        public Task PublishAsync(string userId, NotificationMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("SignalR unavailable");
    }
}
