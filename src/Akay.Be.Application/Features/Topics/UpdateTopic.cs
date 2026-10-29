using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Topics;

public sealed record UpdateTopicCommand([property: JsonIgnore] int Id,
                                        string Name,
                                        string? Description,
                                        bool IsActive) : ICommand;

internal sealed class UpdateTopicCommandHandler(IAdminScopeService adminScope,
                                                IUnitOfWork unitOfWork,
                                                ITopicRepository topicRepository) : ICommandHandler<UpdateTopicCommand>
{
    public async ValueTask<Result> Handle(UpdateTopicCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = await topicRepository.GetByIdAsync(request.Id, cancellationToken);
        if (topic is null)
            return Error.NotFound("topic.not_found", $"Tema {request.Id} no encontrado.");

        var access = await adminScope.EnsureCanWriteSubjectContentAsync(topic.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        topic.ChangeName(request.Name);
        topic.ChangeDescription(request.Description);
        topic.ChangeActivation(request.IsActive);

        topicRepository.Update(topic);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class UpdateTopicCommandValidator : AbstractValidator<UpdateTopicCommand>
{
    public UpdateTopicCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
