using Akay.To.Core.Application.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs;

public sealed record SyncLearningHubCommand(int Id) : ICommand<LearningHubResponse>, IRetryableRequest
{
    public int RetryCount => 3;

    public TimeSpan BaseDelay => TimeSpan.FromMilliseconds(200);
}

internal static class SyncAttemptTracker
{
    private static int _count;
    private static readonly Lock Lock = new();

    public static int Next()
    {
        lock (Lock)
        {
            return ++_count;
        }
    }

    public static void Reset()
    {
        lock (Lock)
        {
            _count = 0;
        }
    }
}

internal sealed class SyncLearningHubCommandHandler : ICommandHandler<SyncLearningHubCommand, LearningHubResponse>
{
    public ValueTask<Result<LearningHubResponse>> Handle(SyncLearningHubCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attempt = SyncAttemptTracker.Next();

        var hub = LearningHubStore.GetById(request.Id);

        if (hub is null)
            return ValueTask.FromResult<Result<LearningHubResponse>>(Error.NotFound("learninghub.not_found", $"Centro de estudios con ID {request.Id} no encontrado."));

        if (attempt % 3 != 0)
            throw new InvalidOperationException($"Error de conexión con fuente externa (intento {attempt}).");

        var response = new LearningHubResponse(hub.Id, hub.Name, hub.Description, hub.Address, hub.Category, hub.Status, hub.CreatedAt, hub.UpdatedAt);

        return ValueTask.FromResult<Result<LearningHubResponse>>(response);
    }
}
