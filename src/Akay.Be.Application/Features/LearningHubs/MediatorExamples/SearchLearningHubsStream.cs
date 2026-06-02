using System.Runtime.CompilerServices;
using Akay.To.Core.Application.Abstractions.Mediator;

namespace Akay.Be.Application.Features.LearningHubs.MediatorExamples;

public sealed record SearchLearningHubsStreamRequest(string SearchTerm) : IStreamQuery<LearningHubStreamItem>;

public sealed record LearningHubStreamItem(int Id, string Name, string Category, string Relevance);

internal sealed class SearchLearningHubsStreamHandler : IStreamRequestHandler<SearchLearningHubsStreamRequest, LearningHubStreamItem>
{
    public async IAsyncEnumerable<LearningHubStreamItem> Handle(SearchLearningHubsStreamRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var term = request.SearchTerm?.Trim();

        var hubs = string.IsNullOrWhiteSpace(term)
            ? LearningHubStore.GetAll().ToList()
            : LearningHubStore.GetAll()
                .Where(h =>
                    h.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || h.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || h.Description.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();

        foreach (var hub in hubs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(1500, cancellationToken).ConfigureAwait(false);

            var relevance = ResolveRelevance(term, hub);

            yield return new LearningHubStreamItem(hub.Id, hub.Name, hub.Category, relevance);
        }
    }

    private static string ResolveRelevance(string? term, LearningHubData hub)
    {
        if (string.IsNullOrWhiteSpace(term))
            return "general";

        if (hub.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            return "alta";

        if (hub.Category.Equals(term, StringComparison.OrdinalIgnoreCase))
            return "media";

        return "baja";
    }
}
