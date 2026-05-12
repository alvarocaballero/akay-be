using Akay.To.Core.Application.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs;

public sealed record DeleteLearningHubCommand(int Id) : ICommand;

internal sealed class DeleteLearningHubCommandHandler : ICommandHandler<DeleteLearningHubCommand>
{
    public ValueTask<Result> Handle(DeleteLearningHubCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = LearningHubStore.GetById(request.Id);

        if (existing is null)
            return ValueTask.FromResult<Result>(Error.NotFound("learninghub.not_found", $"Centro de estudios con ID {request.Id} no encontrado."));

        LearningHubStore.Delete(request.Id);

        return ValueTask.FromResult(Result.Success());
    }
}
