using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Notifications;

public sealed record MarkAllNotificationsReadCommand : ICommand;

internal sealed class MarkAllNotificationsReadHandler(IUserContext user, INotificationRepository repository)
    : ICommandHandler<MarkAllNotificationsReadCommand>
{
    public async ValueTask<Result> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var identity = NotificationAccess.GetUserId(user);

        if (identity.IsFailure)
            return identity.Error;

        await repository.MarkAllReadAsync(identity.Value, DateTimeOffset.UtcNow, cancellationToken);

        return Result.Success();
    }
}
