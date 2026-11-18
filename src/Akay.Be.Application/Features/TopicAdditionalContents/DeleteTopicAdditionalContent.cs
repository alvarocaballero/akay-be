using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;

using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.TopicAdditionalContents;

public sealed record DeleteTopicAdditionalContentCommand(int TopicId, int Id) : ICommand;

internal sealed class DeleteTopicAdditionalContentCommandHandler(IAdminScopeService adminScope,
                                                                IUnitOfWork unitOfWork,
                                                                ITopicRepository topicRepository) : ICommandHandler<DeleteTopicAdditionalContentCommand>
{
    public async ValueTask<Result> Handle(DeleteTopicAdditionalContentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = await topicRepository.GetWithAdditionalContentsAsync(request.TopicId, cancellationToken);
        if (topic is null)
            return Error.NotFound("topic.not_found", $"Tema {request.TopicId} no encontrado.");

        var access = await adminScope.EnsureCanWriteSubjectContentAsync(topic.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var content = topic.AdditionalContents.SingleOrDefault(x => x.Id == request.Id);
        if (content is null)
            return Error.NotFound("topic_additional_content.not_found", $"Contenido adicional {request.Id} no encontrado.");

        topicRepository.RemoveAdditionalContent(topic, content);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
