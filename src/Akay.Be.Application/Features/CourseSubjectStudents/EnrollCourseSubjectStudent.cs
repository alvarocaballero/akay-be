using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.CourseSubjectStudents;

public sealed record EnrollCourseSubjectStudentCommand(int CourseId, int SubjectId, int StudentId) : ICommand<CreatedResponse<int>>;

internal sealed class EnrollCourseSubjectStudentCommandHandler(IAdminScopeService adminScope,
                                                               IUnitOfWork unitOfWork,
                                                               ICourseRepository courseRepository) : ICommandHandler<EnrollCourseSubjectStudentCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(EnrollCourseSubjectStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessCourseAsync(request.CourseId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.CourseId, cancellationToken);
        if (course is null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        var courseSubject = course.Subjects.FirstOrDefault(s => s.SubjectId == request.SubjectId);
        if (courseSubject is null)
            return Error.NotFound("course.subject_not_found", "La asignatura no está asignada a este curso.");

        var studentCourse = course.Students.FirstOrDefault(s => s.StudentId == request.StudentId);
        if (studentCourse is null)
            return Error.Forbidden("course.subject.student_not_enrolled", "El estudiante debe estar matriculado previamente en el curso.");

        courseSubject.EnrollStudent(studentCourse.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(studentCourse.Id, studentCourse.CreatedAt);
    }
}

public sealed class EnrollCourseSubjectStudentCommandValidator : AbstractValidator<EnrollCourseSubjectStudentCommand>
{
    public EnrollCourseSubjectStudentCommandValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.StudentId).GreaterThan(0);
    }
}
