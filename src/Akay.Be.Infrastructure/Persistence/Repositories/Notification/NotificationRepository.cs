using System.Transactions;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.Be.Domain.Entities.Notification;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.EF.Infrastructure.Queries;
using Akay.To.EF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Notification;

public sealed class NotificationRepository(ApplicationDbContext context, IServiceScopeFactory scopes)
    : BaseRepository<PersistentNotification, int>(context), INotificationRepository
{
    public async Task PersistAsync(PersistentNotification notification, CancellationToken cancellationToken)
    {
        if (Transaction.Current is not null)
            throw new InvalidOperationException("Send notifications after committing the business transaction.");

        // Keep notification SaveChanges independent from pending business entities and local transactions.
        await using var scope = scopes.CreateAsyncScope();

        scope.ServiceProvider.GetRequiredService<INotificationRepository>().Add(notification);
        _ = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
    }

    public Task<PagedResponse<List<PersistentNotification>>> GetPagedAsync(int userId,
                                                               int page,
                                                               int pageSize,
                                                               CancellationToken cancellationToken)
    {
        return Queryable.AsNoTracking()
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .ToPagedResponseAsync(page, pageSize, cancellationToken: cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken) =>
        Queryable.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);

    public async Task<bool> MarkReadAsync(int userId, int id, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        var utc = readAt.ToUniversalTime();
        var affected = await Queryable.Where(n => n.UserId == userId && n.Id == id)
                                      .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, n => n.ReadAt ?? utc),
                                                          cancellationToken);
        return affected > 0;
    }

    public async Task MarkAllReadAsync(int userId, DateTimeOffset readAt, CancellationToken cancellationToken)
    {
        var utc = readAt.ToUniversalTime();
        _ = await Queryable.Where(n => n.UserId == userId && n.ReadAt == null)
                       .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, utc), cancellationToken);
    }

    public async Task<bool> DeleteForUserAsync(int userId, int id, CancellationToken cancellationToken) =>
        await Queryable.Where(n => n.UserId == userId && n.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
