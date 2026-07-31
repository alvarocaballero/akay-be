using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.CourseSubjectTeachers;

public sealed record RemoveCourseSubjectTeacherCommand(int CourseId, int SubjectId, int UserId) : ICommand;

internal sealed class RemoveCourseSubjectTeacherCommandHandler(IAdminScopeService adminScope,
                                                               IUnitOfWork unitOfWork,
                                                               ICourseRepository courseRepository) : ICommandHandler<RemoveCourseSubjectTeacherCommand>
{
    public async ValueTask<Result> Handle(RemoveCourseSubjectTeacherCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteCourseAsync(request.CourseId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.CourseId, cancellationToken: cancellationToken);
        if (course is null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        var courseSubject = course.Subjects.FirstOrDefault(s => s.SubjectId == request.SubjectId);
        if (courseSubject is null)
            return Error.NotFound("course.subject_not_found", "La asignatura no está asignada a este curso.");

        courseSubject.RemoveTeacher(request.UserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
