using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Subjects;

public sealed record RemoveSubjectAdminCommand(int SubjectId, int UserId) : ICommand;

internal sealed class RemoveSubjectAdminCommandHandler(IAdminScopeService adminScope,
                                                       IUnitOfWork unitOfWork,
                                                       ISubjectRepository subjectRepository) : ICommandHandler<RemoveSubjectAdminCommand>
{
    public async ValueTask<Result> Handle(RemoveSubjectAdminCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteSubjectAsync(request.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var subject = await subjectRepository.GetWithAdminsAsync(request.SubjectId, cancellationToken);
        if (subject is null || subject.DeletedAt is not null)
            return Error.NotFound("subject.not_found", $"Asignatura {request.SubjectId} no encontrada.");

        subject.RemoveAdmin(request.UserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}

public sealed class RemoveSubjectAdminCommandValidator : AbstractValidator<RemoveSubjectAdminCommand>
{
    public RemoveSubjectAdminCommandValidator()
    {
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
