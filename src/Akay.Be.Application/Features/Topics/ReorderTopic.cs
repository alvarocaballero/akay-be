using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Topics;

public sealed record ReorderTopicCommand([property: JsonIgnore] int Id,
                                         int DisplayOrder) : ICommand;

internal sealed class ReorderTopicCommandHandler(IAdminScopeService adminScope,
                                                 IUnitOfWork unitOfWork,
                                                 ITopicRepository topicRepository) : ICommandHandler<ReorderTopicCommand>
{
    public async ValueTask<Result> Handle(ReorderTopicCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = await topicRepository.GetByIdAsync(request.Id, cancellationToken);
        if (topic is null)
            return Error.NotFound("topic.not_found", $"Tema {request.Id} no encontrado.");

        var access = await adminScope.EnsureCanWriteSubjectContentAsync(topic.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var topics = await topicRepository.GetBySubjectIdAsync(topic.SubjectId, cancellationToken);
        var orderChange = TopicOrdering.MoveTo(topics, topic, request.DisplayOrder);

        if (orderChange == TopicOrderChange.TopicNotFound)
            return Error.NotFound("topic.not_found", $"Tema {request.Id} no encontrado.");

        if (orderChange == TopicOrderChange.InvalidDisplayOrder)
            return Error.Validation("topic.invalid_display_order",
                                    $"El orden debe estar entre 1 y {topics.Count}.");

        if (orderChange == TopicOrderChange.Unchanged)
            return Result.Success();

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class ReorderTopicCommandValidator : AbstractValidator<ReorderTopicCommand>
{
    public ReorderTopicCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.DisplayOrder).GreaterThan(0);
    }
}
