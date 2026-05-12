using Akay.To.Core.Application.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs;

public sealed record GetCachedLearningHubQuery(int Id) : IQuery<LearningHubResponse>, ICacheable<LearningHubResponse>
{
    public string CacheKey => $"learninghub:{Id}";

    public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);
}

internal sealed class GetCachedLearningHubQueryHandler : IQueryHandler<GetCachedLearningHubQuery, LearningHubResponse>
{
    public ValueTask<Result<LearningHubResponse>> Handle(GetCachedLearningHubQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hub = LearningHubStore.GetById(request.Id);

        return hub is null
            ? ValueTask.FromResult<Result<LearningHubResponse>>(Error.NotFound("learninghub.not_found", $"Centro de estudios con ID {request.Id} no encontrado."))
            : ValueTask.FromResult<Result<LearningHubResponse>>(new LearningHubResponse(hub.Id,
                                                                                        hub.Name,
                                                                                        hub.Description,
                                                                                        hub.Address,
                                                                                        hub.Category,
                                                                                        hub.Status,
                                                                                        hub.CreatedAt,
                                                                                        hub.UpdatedAt));
    }
}
