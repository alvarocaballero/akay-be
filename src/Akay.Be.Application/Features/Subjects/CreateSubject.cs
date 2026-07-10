using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Subjects;

public sealed record CreateSubjectCommand(string Name, string? Description, IReadOnlyList<int> CenterIds) : ICommand<CreatedResponse<int>>;

internal sealed class CreateSubjectCommandHandler(IAdminScopeService adminScope,
                                                  IUnitOfWork unitOfWork,
                                                  ISubjectRepository subjectRepository) : ICommandHandler<CreateSubjectCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCheck = await adminScope.EnsureAdminOfAllCentersAsync(request.CenterIds, cancellationToken);
        if (adminCheck.IsFailure)
            return adminCheck.Error;

        var subject = Domain.Entities.Academic.Subject.Create(request.Name, request.Description, request.CenterIds);
        subjectRepository.Add(subject);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(subject.Id, subject.CreatedAt);
    }
}

public sealed class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CenterIds).NotEmpty().ForEach(id => id.GreaterThan(0));
    }
}
