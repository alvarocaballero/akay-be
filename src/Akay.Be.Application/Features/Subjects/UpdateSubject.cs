using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Subjects;

public sealed record UpdateSubjectCommand([property: JsonIgnore] int Id, string Name, string? Description) : ICommand<SubjectResponse>;

internal sealed class UpdateSubjectCommandHandler(IAdminScopeService adminScope,
                                                  IUnitOfWork unitOfWork,
                                                  ISubjectRepository subjectRepository) : ICommandHandler<UpdateSubjectCommand, SubjectResponse>
{
    public async ValueTask<Result<SubjectResponse>> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteSubjectAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var subject = await subjectRepository.GetWithCentersAsync(request.Id, cancellationToken);
        if (subject is null)
            return Error.NotFound("subject.not_found", $"Asignatura {request.Id} no encontrada.");

        subject.ChangeName(request.Name);
        subject.ChangeDescription(request.Description);

        subjectRepository.Update(subject);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubjectResponse(
            subject.Id,
            subject.Name,
            subject.Description,
            subject.Centers.Select(c => c.CenterId).ToList());
    }
}

public sealed class UpdateSubjectCommandValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}
