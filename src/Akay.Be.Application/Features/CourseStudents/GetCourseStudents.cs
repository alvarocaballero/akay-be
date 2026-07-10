using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.CourseStudents;

public sealed record GetCourseStudentsQuery(int CourseId) : IQuery<IReadOnlyList<CourseStudentResponse>>;

internal sealed class GetCourseStudentsQueryHandler(IAdminScopeService adminScope,
                                                    ICourseRepository courseRepository) : IQueryHandler<GetCourseStudentsQuery, IReadOnlyList<CourseStudentResponse>>
{
    public async ValueTask<Result<IReadOnlyList<CourseStudentResponse>>> Handle(GetCourseStudentsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessCourseAsync(request.CourseId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var courseExists = await courseRepository.ExistsAsync(request.CourseId, cancellationToken);
        if (!courseExists)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        return await courseRepository.GetStudentsWithUsersByCourseAsync(request.CourseId, cancellationToken);
    }
}
