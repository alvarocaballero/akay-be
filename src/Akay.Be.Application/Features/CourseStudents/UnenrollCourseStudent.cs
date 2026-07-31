using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.CourseStudents;

public sealed record UnenrollCourseStudentCommand(int CourseId, int StudentId) : ICommand;

internal sealed class UnenrollCourseStudentCommandHandler(IAdminScopeService adminScope,
                                                          IUnitOfWork unitOfWork,
                                                          ICourseRepository courseRepository) : ICommandHandler<UnenrollCourseStudentCommand>
{
    public async ValueTask<Result> Handle(UnenrollCourseStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteCourseAsync(request.CourseId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithStudentsAsync(request.CourseId, cancellationToken: cancellationToken);
        if (course is null || course.DeletedAt is not null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        course.UnenrollStudent(request.StudentId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
