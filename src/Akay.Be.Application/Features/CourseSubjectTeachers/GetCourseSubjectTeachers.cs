using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.CourseSubjectTeachers;

public sealed record GetCourseSubjectTeachersQuery(int CourseId, int SubjectId) : IQuery<IReadOnlyList<CourseSubjectTeacherResponse>>;

internal sealed class GetCourseSubjectTeachersQueryHandler(IAdminScopeService adminScope,
                                                           ICourseRepository courseRepository) : IQueryHandler<GetCourseSubjectTeachersQuery, IReadOnlyList<CourseSubjectTeacherResponse>>
{
    public async ValueTask<Result<IReadOnlyList<CourseSubjectTeacherResponse>>> Handle(GetCourseSubjectTeachersQuery request, CancellationToken cancellationToken)
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

        var teachers = await courseRepository.GetCourseSubjectTeachersWithDetailsAsync(request.CourseId,
                                                                                       request.SubjectId,
                                                                                       cancellationToken);
        return teachers.AsReadOnly();
    }
}
