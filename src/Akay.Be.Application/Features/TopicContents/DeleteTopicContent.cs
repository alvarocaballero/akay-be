using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.TopicContents;

public sealed record DeleteTopicContentCommand(int TopicId, int Id) : ICommand;

internal sealed class DeleteTopicContentCommandHandler(IAdminScopeService adminScope,
                                                      IUnitOfWork unitOfWork,
                                                      ITopicRepository topicRepository,
                                                      ITopicContentRepository topicContentRepository) : ICommandHandler<DeleteTopicContentCommand>
{
    public async ValueTask<Result> Handle(DeleteTopicContentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = await topicRepository.GetByIdAsync(request.TopicId, cancellationToken);
        if (topic is null)
            return Error.NotFound("topic.not_found", $"Tema {request.TopicId} no encontrado.");

        var access = await adminScope.EnsureCanWriteSubjectContentAsync(topic.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var content = await topicContentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (content is null || content.TopicId != topic.Id)
            return Error.NotFound("topic_content.not_found", $"Contenido {request.Id} no encontrado.");

        topicContentRepository.Remove(content);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
