using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.CourseStudents;

public sealed record EnrollCourseStudentCommand(int CourseId, int StudentId, int[]? SubjectIds = null) : ICommand<CreatedResponse<int>>;

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

        var student = await studentRepository.GetByIdAsync(request.StudentId, cancellationToken);
        if (student is null || student.DeletedAt is not null)
            return Error.NotFound("student.not_found", $"Estudiante {request.StudentId} no encontrado.");

        if (student.CenterId != course.AcademicPeriod.CenterId)
            return Error.Forbidden("course.student_wrong_center", "El estudiante debe pertenecer al mismo centro que el curso.");


        course.EnrollStudent(request.StudentId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var studentCourse = course.Students.First(s => s.StudentId == request.StudentId && s.DeletedAt == null);

        var enrolledInSubjects = false;
        if (request.SubjectIds is null)
        {
            foreach (var courseSubject in course.Subjects.Where(s => s.DeletedAt == null))
            {
                courseSubject.EnrollStudent(studentCourse.Id);
                enrolledInSubjects = true;
            }
        }
        else if (request.SubjectIds.Length > 0)
        {
            var subjectIdSet = request.SubjectIds.ToHashSet();
            foreach (var courseSubject in course.Subjects.Where(s => s.DeletedAt == null && subjectIdSet.Contains(s.SubjectId)))
            {
                courseSubject.EnrollStudent(studentCourse.Id);
                enrolledInSubjects = true;
            }
        }

        if (enrolledInSubjects)
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(studentCourse.Id, studentCourse.CreatedAt);
    }
}

public sealed class EnrollCourseStudentCommandValidator : AbstractValidator<EnrollCourseStudentCommand>
{
    public EnrollCourseStudentCommandValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.StudentId).GreaterThan(0);
        When(x => x.SubjectIds is not null, () => RuleForEach(x => x.SubjectIds).GreaterThan(0));
    }
}
