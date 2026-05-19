using Akay.Be.Application.Features.LearningHubs.Responses;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs;

public sealed record GetLearningHubQuery(int Id) : IQuery<LearningHubResponse>, ICacheable<LearningHubResponse>
{
    public string CacheKey => $"learninghub:{Id}";

    public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);
}

internal sealed class GetLearningHubQueryHandler : IQueryHandler<GetLearningHubQuery, LearningHubResponse>
{
    public async ValueTask<Result<LearningHubResponse>> Handle(GetLearningHubQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hub = LearningHubStore.GetById(request.Id);

        return hub is null
            ? Error.NotFound("learninghub.not_found", $"Centro de estudios con ID {request.Id} no encontrado.")
            : new LearningHubResponse(hub.Id, hub.Name, hub.Description, hub.Address, hub.Category, hub.Status, hub.CreatedAt, hub.UpdatedAt);
    }
}
