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

        var currentPosition = topics.FindIndex(candidate => candidate.Id == topic.Id);
        if (currentPosition < 0)
            return Error.NotFound("topic.not_found", $"Tema {request.Id} no encontrado.");

        if (request.DisplayOrder < 1 || request.DisplayOrder > topics.Count)
            return Error.Validation("topic.invalid_display_order",
                                    $"El orden debe estar entre 1 y {topics.Count}.");

        if (currentPosition == request.DisplayOrder - 1)
            return Result.Success();

        var reordered = topics
            .Where(candidate => candidate.Id != topic.Id)
            .ToList();

        reordered.Insert(request.DisplayOrder - 1, topic);

        for (var index = 0; index < reordered.Count; index++)
        {
            reordered[index].ChangeDisplayOrder(index + 1);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

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
