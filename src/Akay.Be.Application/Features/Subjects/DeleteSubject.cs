using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Subjects;

public sealed record DeleteSubjectCommand(int Id) : ICommand;

internal sealed class DeleteSubjectCommandHandler(IAdminScopeService adminScope,
                                                  IUnitOfWork unitOfWork,
                                                  ISubjectRepository subjectRepository) : ICommandHandler<DeleteSubjectCommand>
{
    public async ValueTask<Result> Handle(DeleteSubjectCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessSubjectAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var subject = await subjectRepository.GetByIdAsync(request.Id, cancellationToken);
        if (subject is null || subject.DeletedAt is not null)
            return Error.NotFound("subject.not_found", $"Asignatura {request.Id} no encontrada.");

        subject.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
