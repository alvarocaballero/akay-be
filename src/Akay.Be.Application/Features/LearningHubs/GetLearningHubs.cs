using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs;

public sealed record GetLearningHubsQuery(string? Category = null, string? Status = null) : IQuery<IReadOnlyList<LearningHubSummary>>;

public sealed record LearningHubSummary(int Id, string Name, string Category, string Status);

internal sealed class GetLearningHubsQueryHandler : IQueryHandler<GetLearningHubsQuery, IReadOnlyList<LearningHubSummary>>
{
    public async ValueTask<Result<IReadOnlyList<LearningHubSummary>>> Handle(GetLearningHubsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<LearningHubData> hubs = LearningHubStore.GetAll();

        if (!string.IsNullOrWhiteSpace(request.Category))
            hubs = hubs.Where(h => string.Equals(h.Category, request.Category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.Status))
            hubs = hubs.Where(h => string.Equals(h.Status, request.Status, StringComparison.OrdinalIgnoreCase));

        return hubs.Select(static h => new LearningHubSummary(h.Id, h.Name, h.Category, h.Status))
                   .ToList();

    }
}
