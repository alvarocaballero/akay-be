using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Students;

public sealed record DeleteStudentCommand(int UserId, int CenterId) : ICommand;

internal sealed class DeleteStudentCommandHandler(IAdminScopeService adminScope,
                                                   IUnitOfWork unitOfWork,
                                                   IStudentRepository studentRepository,
                                                   ICourseRepository courseRepository) : ICommandHandler<DeleteStudentCommand>
{
    public async ValueTask<Result> Handle(DeleteStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteStudentAsync(request.UserId, request.CenterId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var student = await studentRepository.GetByUserIdAndCenterIdAsync(request.UserId, request.CenterId, cancellationToken);
        if (student is null || student.DeletedAt is not null)
            return Error.NotFound("student.not_found", $"Estudiante {request.UserId} no encontrado en el centro {request.CenterId}.");

        student.SoftDelete();
        studentRepository.Update(student);
        await courseRepository.SoftDeleteStudentEnrollmentsAsync(request.UserId, request.CenterId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
