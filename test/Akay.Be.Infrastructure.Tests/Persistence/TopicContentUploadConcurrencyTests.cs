using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.TopicContents;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Services;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.EF.Infrastructure.DbContexts;
using Akay.To.EF.Infrastructure.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akay.Be.Infrastructure.Tests.Persistence;

public sealed class TopicContentUploadConcurrencyTests
{
    [Fact]
    public async Task RecoveryFailsOnlyExpiredUploadsAndNotifiesTheOwner()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = Guid.NewGuid().ToString();
        await using var setup = CreateContext(name);
        var topic = Topic.Create(1, "Topic");
        var stale = topic.AddContent("stale.pdf", "application/pdf");
        var active = topic.AddContent("active.pdf", "application/pdf");
        var missingUser = topic.AddContent("unknown.pdf", "application/pdf");
        setup.Topics.Add(topic);
        setup.Entry(stale).Property(x => x.CreatedAt).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-10);
        setup.Entry(stale).Property(x => x.CreatedBy).CurrentValue = "7";
        setup.Entry(active).Property(x => x.CreatedAt).CurrentValue = DateTimeOffset.UtcNow;
        setup.Entry(active).Property(x => x.CreatedBy).CurrentValue = "7";
        setup.Entry(missingUser).Property(x => x.CreatedAt).CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-10);
        setup.Entry(missingUser).Property(x => x.CreatedBy).CurrentValue = "not-a-user-id";
        await setup.SaveChangesAsync(cancellationToken);

        var notifications = new RecordingNotifications();
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateContext(name));
        services.AddSingleton<INotificationService>(notifications);
        await using var provider = services.BuildServiceProvider();
        var worker = new InterruptedTopicContentUploadWorker(provider.GetRequiredService<IServiceScopeFactory>(),
                                                             NullLogger<InterruptedTopicContentUploadWorker>.Instance);
        await worker.RecoverAsync(cancellationToken);

        await using var verify = CreateContext(name);
        var states = await verify.TopicContents.OrderBy(x => x.Id).Select(x => x.ProcessingState).ToListAsync(cancellationToken);
        Assert.Equal([ContentProcessingState.UploadFailed, ContentProcessingState.Uploading, ContentProcessingState.UploadFailed], states);
        var notification = Assert.IsType<TopicContentStateChanged>(Assert.Single(notifications.Items));
        Assert.Equal(7, notification.UserId);
        Assert.Equal(ContentProcessingState.UploadFailed, notification.State);
    }

    [Fact]
    public async Task InterruptedUploadCannotOverwriteRecovery()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var name = Guid.NewGuid().ToString();
        await using var setup = CreateContext(name);
        var topic = Topic.Create(1, "Topic");
        var content = topic.AddContent("file.pdf", "application/pdf");
        setup.Topics.Add(topic);
        await setup.SaveChangesAsync(cancellationToken);

        await using var oldAttempt = CreateContext(name);
        await using var recovery = CreateContext(name);
        var oldContent = await oldAttempt.TopicContents.SingleAsync(cancellationToken);
        var recovered = await recovery.TopicContents.SingleAsync(cancellationToken);
        recovered.FailUpload("Interrupted");
        await recovery.SaveChangesAsync(cancellationToken);
        oldContent.CompleteUpload("https://example.invalid/old-attempt");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => oldAttempt.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task AuditingStampsCreatedByOnTopicContent()
    {
        var user = new AuthenticatedUser();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditingSaveChangesInterceptor(TimeProvider.System, user))
            .Options;
        var registration = new DbContextRegistration<ApplicationDbContext>(new DbContextSettings(), (_, _, _) => { });
        await using var context = new ApplicationDbContext(user, registration, options);
        var topic = Topic.Create(1, "Topic");
        var content = topic.AddContent("file.pdf", "application/pdf");
        context.Topics.Add(topic);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("7", content.CreatedBy);
        Assert.NotEqual(default, content.CreatedAt);
    }

    private static ApplicationDbContext CreateContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name).Options;
        var registration = new DbContextRegistration<ApplicationDbContext>(new DbContextSettings(), (_, _, _) => { });
        return new ApplicationDbContext(new TestUserContext(), registration, options);
    }

    private sealed class RecordingNotifications : INotificationService
    {
        public List<INotification> Items { get; } = [];

        public Task SendAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Items.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class AuthenticatedUser : IUserContext
    {
        public bool IsAuthenticated => true;
        public int UserId => 7;
        public string Name => string.Empty;
        public string Email => string.Empty;
        public IEnumerable<string> Roles => [];
    }
}
