using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.Be.Application.Notifications;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Notifications;

public sealed record MarkNotificationReadCommand(int Id) : ICommand;

internal sealed class MarkNotificationReadHandler(IUserContext user, INotificationRepository repository)
    : ICommandHandler<MarkNotificationReadCommand>
{
    public async ValueTask<Result> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var identity = NotificationAccess.GetUserId(user);

        if (identity.IsFailure)
            return identity.Error;

        return await repository.MarkReadAsync(identity.Value, request.Id, DateTimeOffset.UtcNow, cancellationToken)
            ? Result.Success() : NotificationAccess.NotFound;
    }
}
