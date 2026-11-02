using Akay.Be.Domain.Entities.Notification;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;

public interface INotificationRepository : IBaseRepository<PersistentNotification, int>
{
    /// <summary>Commits independently of the caller's tracked business entities.</summary>
    Task PersistAsync(PersistentNotification notification, CancellationToken cancellationToken);

    Task<PagedResponse<List<PersistentNotification>>> GetPagedAsync(int userId,
                                                              int page,
                                                              int pageSize,
                                                              CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(int userId, int id, DateTimeOffset readAt, CancellationToken cancellationToken);

    Task MarkAllReadAsync(int userId, DateTimeOffset readAt, CancellationToken cancellationToken);

    Task<bool> DeleteForUserAsync(int userId, int id, CancellationToken cancellationToken);
}
