using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Courses;

public sealed record GetCourseByIdQuery(int Id) : IQuery<CourseResponse>;

internal sealed class GetCourseByIdQueryHandler(IAdminScopeService adminScope,
                                                ICourseRepository courseRepository) : IQueryHandler<GetCourseByIdQuery, CourseResponse>
{
    public async ValueTask<Result<CourseResponse>> Handle(GetCourseByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessCourseAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.Id, readOnly: true, cancellationToken);
        if (course is null)
            return Error.NotFound("course.not_found", $"Curso {request.Id} no encontrado.");

        return CourseMapper.ToResponse(course);
    }
}
