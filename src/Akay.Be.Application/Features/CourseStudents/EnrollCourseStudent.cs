using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.CourseStudents;

public sealed record EnrollCourseStudentCommand(int CourseId, int StudentId) : ICommand<CreatedResponse<int>>;

internal sealed class EnrollCourseStudentCommandHandler(IAdminScopeService adminScope,
                                                        IUnitOfWork unitOfWork,
                                                        ICourseRepository courseRepository,
                                                        IStudentRepository studentRepository) : ICommandHandler<EnrollCourseStudentCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(EnrollCourseStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessCourseAsync(request.CourseId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.CourseId, cancellationToken);
        if (course is null || course.DeletedAt is not null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        var centerId = course.AcademicPeriod.CenterId;

        var adminCheck = await adminScope.EnsureAdminOfCenterAsync(centerId, cancellationToken);
        if (adminCheck.IsFailure)
            return adminCheck.Error;


        var student = await studentRepository.GetByIdAsync(request.StudentId, cancellationToken);
        if (student is null || student.DeletedAt is not null)
            return Error.NotFound("student.not_found", $"Estudiante {request.StudentId} no encontrado.");

        if (student.CenterId != centerId)
            return Error.Forbidden("course.student_wrong_center", "El estudiante debe pertenecer al mismo centro que el curso.");


        course.EnrollStudent(request.StudentId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var enrollment = course.Students.First(s => s.StudentId == request.StudentId && s.DeletedAt == null);
        return new CreatedResponse<int>(enrollment.Id, enrollment.CreatedAt);
    }
}

public sealed class EnrollCourseStudentCommandValidator : AbstractValidator<EnrollCourseStudentCommand>
{
    public EnrollCourseStudentCommandValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.StudentId).GreaterThan(0);
    }
}
