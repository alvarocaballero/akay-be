using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.TopicContents;

public sealed record GetTopicContentsQuery(int TopicId) : IQuery<IReadOnlyList<TopicContentResponse>>;

internal sealed class GetTopicContentsQueryHandler(IAdminScopeService adminScope,
                                                   ITopicRepository topicRepository,
                                                   ITopicContentRepository topicContentRepository) : IQueryHandler<GetTopicContentsQuery, IReadOnlyList<TopicContentResponse>>
{
    public async ValueTask<Result<IReadOnlyList<TopicContentResponse>>> Handle(GetTopicContentsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = await topicRepository.GetByIdAsync(request.TopicId, cancellationToken);
        if (topic is null)
            return Error.NotFound("topic.not_found", $"Tema {request.TopicId} no encontrado.");

        var access = await adminScope.EnsureCanReadSubjectContentAsync(topic.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var contents = await topicContentRepository.GetByTopicIdAsync(topic.Id, cancellationToken);

        return contents
            .Select(content => new TopicContentResponse(content.Id,
                                                        content.TopicId,
                                                        content.FileName,
                                                        content.ContentType,
                                                        content.OriginalContentUrl,
                                                        content.ContentUrl,
                                                        content.ProcessingState,
                                                        content.IndexingState,
                                                        content.ProcessingError,
                                                        content.IndexingError,
                                                        content.ProcessingAttemptId))
            .ToList();
    }
}
