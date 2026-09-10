using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.CourseStudents;

public sealed record EnrollCourseStudentCommand(int CourseId, int UserId, int[]? SubjectIds = null) : ICommand<CreatedResponse<int>>;

internal sealed class EnrollCourseStudentCommandHandler(IAdminScopeService adminScope,
                                                        IUnitOfWork unitOfWork,
                                                        ICourseRepository courseRepository,
                                                        IStudentRepository studentRepository) : ICommandHandler<EnrollCourseStudentCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(EnrollCourseStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteCourseAsync(request.CourseId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.CourseId, cancellationToken: cancellationToken);
        if (course is null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        var students = await studentRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var student = students.FirstOrDefault(x => x.CenterId == course.AcademicPeriod.CenterId);
        if (student is null)
        {
            return students.Count > 0
                ? Error.Forbidden("course.student_wrong_center", "El estudiante debe pertenecer al mismo centro que el curso.")
                : Error.NotFound("student.not_found", $"Estudiante {request.UserId} no encontrado.");
        }

        var enrollment = course.EnrollStudent(student, request.SubjectIds);
        if (!enrollment.Changed)
            return Error.Conflict("course.student_already_enrolled", $"El usuario {request.UserId} ya está matriculado en el curso {request.CourseId}.");

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CreatedResponse<int>(enrollment.StudentCourse.Id, enrollment.StudentCourse.CreatedAt);
    }
}

public sealed class EnrollCourseStudentCommandValidator : AbstractValidator<EnrollCourseStudentCommand>
{
    public EnrollCourseStudentCommandValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
        When(x => x.SubjectIds is not null, () => RuleForEach(x => x.SubjectIds).GreaterThan(0));
    }
}
