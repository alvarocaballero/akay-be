using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Topics;

public sealed record CreateTopicCommand([property: JsonIgnore] int SubjectId,
                                        string Name,
                                        string? Description) : ICommand<CreatedResponse<int>>;

internal sealed class CreateTopicCommandHandler(IAdminScopeService adminScope,
                                                IUnitOfWork unitOfWork,
                                                ITopicRepository topicRepository) : ICommandHandler<CreateTopicCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(CreateTopicCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteSubjectContentAsync(request.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var lastDisplayOrder = await topicRepository.GetMaxDisplayOrderAsync(request.SubjectId, cancellationToken);

        var topic = Topic.Create(request.SubjectId,
                                 request.Name,
                                 request.Description,
                                 lastDisplayOrder + 1);

        topicRepository.Add(topic);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(topic.Id, topic.CreatedAt);
    }
}

public sealed class CreateTopicCommandValidator : AbstractValidator<CreateTopicCommand>
{
    public CreateTopicCommandValidator()
    {
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
