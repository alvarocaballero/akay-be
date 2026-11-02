using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Topics;

public sealed record DeleteTopicCommand(int Id) : ICommand;

internal sealed class DeleteTopicCommandHandler(IAdminScopeService adminScope,
                                                IUnitOfWork unitOfWork,
                                                ITopicRepository topicRepository) : ICommandHandler<DeleteTopicCommand>
{
    public async ValueTask<Result> Handle(DeleteTopicCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = await topicRepository.GetByIdAsync(request.Id, cancellationToken);
        if (topic is null)
            return Error.NotFound("topic.not_found", $"Tema {request.Id} no encontrado.");

        var access = await adminScope.EnsureCanWriteSubjectContentAsync(topic.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var topics = await topicRepository.GetBySubjectIdAsync(topic.SubjectId, cancellationToken);

        topicRepository.Remove(topic);
        TopicOrdering.Remove(topics, topic);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
