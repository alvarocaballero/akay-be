using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.CourseSubjectTeachers;

public sealed record AssignCourseSubjectTeacherCommand(int CourseId, int SubjectId, int UserId) : ICommand<CreatedResponse<int>>;

internal sealed class AssignCourseSubjectTeacherCommandHandler(IAdminScopeService adminScope,
                                                               IUnitOfWork unitOfWork,
                                                               ICourseRepository courseRepository) : ICommandHandler<AssignCourseSubjectTeacherCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(AssignCourseSubjectTeacherCommand request, CancellationToken cancellationToken)
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

        var centerId = course.AcademicPeriod.CenterId;
        var isTeacher = await adminScope.UserHasRoleInCenterAsync(request.UserId, centerId, UserRole.Teacher, cancellationToken);
        if (!isTeacher)
            return Error.Forbidden("course.subject.teacher_not_eligible", "El usuario debe tener rol Teacher en el centro del curso.");

        courseSubject.AssignTeacher(request.UserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var assignment = courseSubject.Teachers.First(t => t.UserId == request.UserId);
        return new CreatedResponse<int>(assignment.UserId, assignment.CreatedAt);
    }
}

public sealed class AssignCourseSubjectTeacherCommandValidator : AbstractValidator<AssignCourseSubjectTeacherCommand>
{
    public AssignCourseSubjectTeacherCommandValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
