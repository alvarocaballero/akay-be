using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.TopicAdditionalContents;

public sealed record GetTopicAdditionalContentsQuery(int TopicId) : IQuery<IReadOnlyList<TopicAdditionalContentResponse>>;

internal sealed class GetTopicAdditionalContentsQueryHandler(IAdminScopeService adminScope,
                                                             ITopicRepository topicRepository,
                                                             ITopicAdditionalContentRepository topicAdditionalContentRepository) : IQueryHandler<GetTopicAdditionalContentsQuery, IReadOnlyList<TopicAdditionalContentResponse>>
{
    public async ValueTask<Result<IReadOnlyList<TopicAdditionalContentResponse>>> Handle(GetTopicAdditionalContentsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = await topicRepository.GetByIdAsync(request.TopicId, cancellationToken);
        if (topic is null)
            return Error.NotFound("topic.not_found", $"Tema {request.TopicId} no encontrado.");

        var access = await adminScope.EnsureCanReadSubjectContentAsync(topic.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var contents = await topicAdditionalContentRepository.GetByTopicIdAsync(topic.Id, cancellationToken);

        return contents
            .Select(content => new TopicAdditionalContentResponse(content.Id,
                                                                 content.TopicId,
                                                                 content.FileName,
                                                                 content.ContentType,
                                                                 content.SourceContentUrl,
                                                                 content.ContentUrl,
                                                                 content.IsVisible))
            .ToList();
    }
}
