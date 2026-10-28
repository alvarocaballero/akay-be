using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Notifications;

public sealed record DeleteNotificationCommand(int Id) : ICommand;

internal sealed class DeleteNotificationHandler(IUserContext user, INotificationRepository repository)
    : ICommandHandler<DeleteNotificationCommand>
{
    public async ValueTask<Result> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        var identity = NotificationAccess.GetUserId(user);

        if (identity.IsFailure)
            return identity.Error;

        return await repository.DeleteForUserAsync(identity.Value, request.Id, cancellationToken)
            ? Result.Success() : NotificationAccess.NotFound;
    }
}
