using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Subjects;

public sealed record AddSubjectCenterCommand([property: JsonIgnore] int SubjectId, int CenterId) : ICommand<CreatedResponse<int>>;

internal sealed class AddSubjectCenterCommandHandler(IAdminScopeService adminScope,
                                                     IUnitOfWork unitOfWork,
                                                     ISubjectRepository subjectRepository) : ICommandHandler<AddSubjectCenterCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(AddSubjectCenterCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessSubjectAsync(request.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var centerCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (centerCheck.IsFailure)
            return centerCheck.Error;

        var subject = await subjectRepository.GetWithCentersAsync(request.SubjectId, cancellationToken);
        if (subject is null)
            return Error.NotFound("subject.not_found", $"Asignatura {request.SubjectId} no encontrada.");

        subject.AddCenter(request.CenterId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(subject.Id, subject.CreatedAt);
    }
}

public sealed class AddSubjectCenterCommandValidator : AbstractValidator<AddSubjectCenterCommand>
{
    public AddSubjectCenterCommandValidator()
    {
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.CenterId).GreaterThan(0);
    }
}
