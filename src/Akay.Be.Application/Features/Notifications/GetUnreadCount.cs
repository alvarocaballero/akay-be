using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Notifications;

public sealed record GetUnreadCountQuery : IQuery<int>;

internal sealed class GetUnreadCountHandler(IUserContext user, INotificationRepository repository)
    : IQueryHandler<GetUnreadCountQuery, int>
{
    public async ValueTask<Result<int>> Handle(GetUnreadCountQuery request, CancellationToken cancellationToken)
    {
        var identity = NotificationAccess.GetUserId(user);

        if (identity.IsFailure)
            return identity.Error;

        return await repository.GetUnreadCountAsync(identity.Value, cancellationToken);
    }
}
