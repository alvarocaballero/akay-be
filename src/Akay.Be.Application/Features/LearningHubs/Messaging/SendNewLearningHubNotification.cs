using Akay.Be.Application.Features.LearningHubs;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs.Messaging;

public sealed record SendNewLearningHubNotification(int Id,
                                                           string Name,
                                                           string Description) : ICommand<Unit>;


internal sealed class SendNewLearningHubNotificationHandler : ICommandHandler<SendNewLearningHubNotification, Unit>
{
    public async ValueTask<Result<Unit>> Handle(SendNewLearningHubNotification request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = LearningHubStore.GetById(request.Id);

        if (existing is null)
            return Error.NotFound("learninghub.not_found", $"Centro de estudios con ID {request.Id} no encontrado.");

        return Result<Unit>.Success(Unit.Value);
    }
}
