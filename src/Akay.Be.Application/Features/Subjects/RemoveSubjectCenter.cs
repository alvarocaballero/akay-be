using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Subjects;

public sealed record RemoveSubjectCenterCommand(int SubjectId, int CenterId) : ICommand<SubjectResponse>;

internal sealed class RemoveSubjectCenterCommandHandler(IAdminScopeService adminScope,
                                                        IUnitOfWork unitOfWork,
                                                        ISubjectRepository subjectRepository) : ICommandHandler<RemoveSubjectCenterCommand, SubjectResponse>
{
    public async ValueTask<Result<SubjectResponse>> Handle(RemoveSubjectCenterCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteSubjectAsync(request.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var centerCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (centerCheck.IsFailure)
            return centerCheck.Error;

        var subject = await subjectRepository.GetWithCentersAsync(request.SubjectId, cancellationToken);
        if (subject is null)
            return Error.NotFound("subject.not_found", $"Asignatura {request.SubjectId} no encontrada.");

        var center = subject.Centers.FirstOrDefault(candidate => candidate.CenterId == request.CenterId);
        if (center is null)
            return Error.NotFound("subject.center_not_found", $"El centro {request.CenterId} no está asociado a esta asignatura.");

        if (subject.Centers.Count <= 1)
            return Error.Conflict("subject.last_center", "No se puede eliminar el último centro de una asignatura activa.");

        subjectRepository.Remove(center);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubjectResponse(
            subject.Id,
            subject.Name,
            subject.Description,
            subject.Centers.Select(c => c.CenterId).ToList());
    }
}

public sealed class RemoveSubjectCenterCommandValidator : AbstractValidator<RemoveSubjectCenterCommand>
{
    public RemoveSubjectCenterCommandValidator()
    {
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.CenterId).GreaterThan(0);
    }
}
