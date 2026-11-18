using System.Globalization;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.TopicContents;
using Akay.Be.Domain.Enums;
using Akay.Be.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akay.Be.Infrastructure.Services;

internal sealed class InterruptedTopicContentUploadWorker(IServiceScopeFactory scopeFactory,
                                                          ILogger<InterruptedTopicContentUploadWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(3));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to recover interrupted topic content uploads");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }

    internal async Task RecoverAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
        var stale = await context.TopicContents
            .Where(x => x.ProcessingState == ContentProcessingState.Uploading && x.CreatedAt < cutoff)
            .Include(x => x.Topic).ThenInclude(x => x.Contents)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var content in stale)
        {
            content.Topic.FailContentUpload(content, "La subida se interrumpió. Elimina el contenido y sube el archivo de nuevo.");
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                context.Entry(content).State = EntityState.Detached;
                continue;
            }

            if (int.TryParse(content.CreatedBy, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) && userId > 0)
            {
                try
                {
                    await notifications.SendAsync(new TopicContentStateChanged(userId, content.TopicId, content.Id, content.ProcessingState), cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "Could not notify interrupted upload {TopicContentId}", content.Id);
                }
            }
        }
    }
}
