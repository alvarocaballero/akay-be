using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs.MediatorExamples;

public sealed record GetLearningHubsQuery : PagedQuery<List<LearningHubSummary>>
{
    public string? Category { get; init; }
    public string? Status { get; init; }
}

public sealed record LearningHubSummary(int Id, string Name, string Category, string Status);

internal sealed class GetLearningHubsQueryHandler : IQueryHandler<GetLearningHubsQuery, PagedResponse<List<LearningHubSummary>>>
{
    public async ValueTask<Result<PagedResponse<List<LearningHubSummary>>>> Handle(GetLearningHubsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<LearningHubData> hubs = LearningHubStore.GetAll();

        if (!string.IsNullOrWhiteSpace(request.Category))
            hubs = hubs.Where(h => string.Equals(h.Category, request.Category, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.Status))
            hubs = hubs.Where(h => string.Equals(h.Status, request.Status, StringComparison.OrdinalIgnoreCase));

        var summaries = hubs.Select(static h => new LearningHubSummary(h.Id, h.Name, h.Category, h.Status));

        summaries = (request.SortBy?.ToLowerInvariant()) switch
        {
            "name" => request.IsAscending ?? false ? summaries.OrderBy(s => s.Name) : summaries.OrderByDescending(s => s.Name),
            "category" => request.IsAscending ?? false ? summaries.OrderBy(s => s.Category) : summaries.OrderByDescending(s => s.Category),
            "status" => request.IsAscending ?? false ? summaries.OrderBy(s => s.Status) : summaries.OrderByDescending(s => s.Status),
            _ => request.IsAscending ?? false ? summaries.OrderBy(s => s.Id) : summaries.OrderByDescending(s => s.Id)
        };

        var list = summaries.ToList();

        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? 10;
        var totalPages = (int)Math.Ceiling(list.Count / (double)pageSize);
        var hasMoreItems = page < totalPages;

        var pagedData = list.Skip((page - 1) * pageSize)
                            .Take(pageSize)
                            .ToList();

        return PagedResponse<List<LearningHubSummary>>.Create(pagedData, pageSize, page, hasMoreItems);
    }
}
