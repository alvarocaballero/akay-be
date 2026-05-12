using Akay.To.Core.Application.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs;

public sealed record GetLearningHubQuery(int Id) : IQuery<LearningHubResponse>;

public sealed record LearningHubResponse(int Id,
                                         string Name,
                                         string Description,
                                         string Address,
                                         string Category,
                                         string Status,
                                         DateTime CreatedAt,
                                         DateTime UpdatedAt);

internal sealed class GetLearningHubQueryHandler : IQueryHandler<GetLearningHubQuery, LearningHubResponse>
{
    public ValueTask<Result<LearningHubResponse>> Handle(GetLearningHubQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hub = LearningHubStore.GetById(request.Id);

        return hub is null
            ? ValueTask.FromResult<Result<LearningHubResponse>>(Error.NotFound("learninghub.not_found", $"Centro de estudios con ID {request.Id} no encontrado."))
            : ValueTask.FromResult<Result<LearningHubResponse>>(new LearningHubResponse(hub.Id, hub.Name, hub.Description, hub.Address, hub.Category, hub.Status, hub.CreatedAt, hub.UpdatedAt));
    }
}
