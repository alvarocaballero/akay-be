using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.CourseSubjectStudents;

public sealed record UnenrollCourseSubjectStudentCommand(int CourseId, int SubjectId, int StudentId) : ICommand;

internal sealed class UnenrollCourseSubjectStudentCommandHandler(IAdminScopeService adminScope,
                                                                 IUnitOfWork unitOfWork,
                                                                 ICourseRepository courseRepository) : ICommandHandler<UnenrollCourseSubjectStudentCommand>
{
    public async ValueTask<Result> Handle(UnenrollCourseSubjectStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessCourseAsync(request.CourseId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.CourseId, cancellationToken: cancellationToken);
        if (course is null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        var courseSubject = course.Subjects.FirstOrDefault(s => s.SubjectId == request.SubjectId);
        if (courseSubject is null)
            return Error.NotFound("course.subject_not_found", "La asignatura no está asignada a este curso.");

        var studentCourse = course.Students.FirstOrDefault(s => s.StudentId == request.StudentId);
        if (studentCourse is null)
            return Error.NotFound("course.student_not_enrolled", "El estudiante no está matriculado en el curso.");

        courseSubject.UnenrollStudent(studentCourse.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
