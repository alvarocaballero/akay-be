using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Topics;

public sealed record GetTopicsBySubjectQuery(int SubjectId) : IQuery<IReadOnlyList<TopicResponse>>;

internal sealed class GetTopicsBySubjectQueryHandler(IAdminScopeService adminScope,
                                                     ITopicRepository topicRepository) : IQueryHandler<GetTopicsBySubjectQuery, IReadOnlyList<TopicResponse>>
{
    public async ValueTask<Result<IReadOnlyList<TopicResponse>>> Handle(GetTopicsBySubjectQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanReadSubjectContentAsync(request.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var topics = await topicRepository.GetBySubjectIdAsync(request.SubjectId, cancellationToken);

        return topics
            .Select(topic => new TopicResponse(topic.Id,
                                               topic.SubjectId,
                                               topic.Name,
                                               topic.Description,
                                               topic.DisplayOrder,
                                               topic.ProcessingState,
                                               topic.IndexingState,
                                               topic.IsActive))
            .ToList();
    }
}
