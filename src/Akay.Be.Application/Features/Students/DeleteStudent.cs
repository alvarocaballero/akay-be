using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Students;

public sealed record DeleteStudentCommand(int Id) : ICommand;

internal sealed class DeleteStudentCommandHandler(IAdminScopeService adminScope,
                                                  IUnitOfWork unitOfWork,
                                                  IStudentRepository studentRepository) : ICommandHandler<DeleteStudentCommand>
{
    public async ValueTask<Result> Handle(DeleteStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessStudentAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var student = await studentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (student is null || student.DeletedAt is not null)
            return Error.NotFound("student.not_found", $"Estudiante {request.Id} no encontrado.");

        student.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
