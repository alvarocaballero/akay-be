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
        if (course is null || course.DeletedAt is not null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        var students = await studentRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var student = students.FirstOrDefault(x => x.CenterId == course.AcademicPeriod.CenterId);
        if (student is null)
        {
            return students.Count > 0
                ? Error.Forbidden("course.student_wrong_center", "El estudiante debe pertenecer al mismo centro que el curso.")
                : Error.NotFound("student.not_found", $"Estudiante {request.UserId} no encontrado.");
        }

        var targetSubjects = ResolveTargetSubjects(course, request.SubjectIds);

        // The course and subject enrollments are saved in two steps, so a
        // previous failure can leave an active course enrollment without its
        // subject rows. Retrying then completes the missing part instead of
        // blowing up on the unique index.
        var existingEnrollment = course.Students.FirstOrDefault(s => s.UserId == request.UserId && s.DeletedAt == null);
        if (existingEnrollment is not null &&
            !targetSubjects.Any(cs => !cs.Students.Any(e => e.StudentCourseId == existingEnrollment.Id && e.DeletedAt == null)))
            return Error.Conflict("course.student_already_enrolled", $"El usuario {request.UserId} ya está matriculado en el curso {request.CourseId}.");

        StudentCourse studentCourse;
        if (existingEnrollment is null)
        {
            course.EnrollStudent(request.UserId);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            studentCourse = course.Students.First(s => s.UserId == request.UserId && s.DeletedAt == null);
        }
        else
        {
            studentCourse = existingEnrollment;
        }

        var enrolledInSubjects = false;
        foreach (var courseSubject in targetSubjects)
        {
            if (courseSubject.Students.Any(e => e.StudentCourseId == studentCourse.Id && e.DeletedAt == null))
                continue;

            courseSubject.EnrollStudent(studentCourse.Id);
            enrolledInSubjects = true;
        }

        if (enrolledInSubjects)
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(studentCourse.Id, studentCourse.CreatedAt);
    }

    private static List<CourseSubject> ResolveTargetSubjects(Course course, int[]? subjectIds)
    {
        var activeSubjects = course.Subjects.Where(s => s.DeletedAt == null);
        return subjectIds switch
        {
            null => activeSubjects.ToList(),
            { Length: 0 } => [],
            _ => activeSubjects.Where(s => subjectIds.Contains(s.SubjectId)).ToList()
        };
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
