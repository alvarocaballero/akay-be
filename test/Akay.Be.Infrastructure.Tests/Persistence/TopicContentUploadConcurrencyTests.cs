using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.TopicContents;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Services;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.EF.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Akay.Be.Infrastructure.Tests.Persistence;

public sealed class TopicContentUploadConcurrencyTests
{
    [Fact]
    public async Task RecoveryFailsOnlyExpiredUploadsAndNotifiesTheOwner()
    {
        var name = Guid.NewGuid().ToString();
        await using var setup = CreateContext(name);
        var topic = Topic.Create(1, "Topic");
        var stale = topic.AddContent("stale.pdf", "application/pdf");
        stale.SetInitialUploadDetails(7, DateTimeOffset.UtcNow.AddMinutes(-10));
        var active = topic.AddContent("active.pdf", "application/pdf");
        active.SetInitialUploadDetails(7, DateTimeOffset.UtcNow);
        setup.Topics.Add(topic);
        await setup.SaveChangesAsync();

        var notifications = new RecordingNotifications();
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateContext(name));
        services.AddSingleton<INotificationService>(notifications);
        await using var provider = services.BuildServiceProvider();
        var worker = new InterruptedTopicContentUploadWorker(provider.GetRequiredService<IServiceScopeFactory>(),
                                                             NullLogger<InterruptedTopicContentUploadWorker>.Instance);
        await worker.RecoverAsync(CancellationToken.None);

        await using var verify = CreateContext(name);
        var states = await verify.TopicContents.OrderBy(x => x.Id).Select(x => x.ProcessingState).ToListAsync();
        Assert.Equal([ContentProcessingState.UploadFailed, ContentProcessingState.Uploading], states);
        var notification = Assert.IsType<TopicContentStateChanged>(Assert.Single(notifications.Items));
        Assert.Equal(7, notification.UserId);
        Assert.Equal(ContentProcessingState.UploadFailed, notification.State);
    }

    [Fact]
    public async Task InterruptedAttemptCannotOverwriteRetriedUpload()
    {
        var name = Guid.NewGuid().ToString();
        await using var setup = CreateContext(name);
        var topic = Topic.Create(1, "Topic");
        var content = topic.AddContent("file.pdf", "application/pdf");
        content.SetInitialUploadDetails(1, DateTimeOffset.UtcNow.AddMinutes(-10));
        setup.Topics.Add(topic);
        await setup.SaveChangesAsync();

        await using var oldAttempt = CreateContext(name);
        await using var recovery = CreateContext(name);
        var oldContent = await oldAttempt.TopicContents.SingleAsync();
        var recovered = await recovery.TopicContents.SingleAsync();
        recovered.FailUpload("Interrupted");
        await recovery.SaveChangesAsync();
        recovered.StartUpload("file.pdf", "application/pdf", 1, DateTimeOffset.UtcNow);
        await recovery.SaveChangesAsync();

        oldContent.CompleteUpload("https://example.invalid/old-attempt");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => oldAttempt.SaveChangesAsync());
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
}
