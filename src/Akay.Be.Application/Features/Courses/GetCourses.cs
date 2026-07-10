using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Courses;

public sealed record GetCoursesQuery : IQuery<IReadOnlyList<CourseListResponse>>;

internal sealed class GetCoursesQueryHandler(IAdminScopeService adminScope,
                                             ICourseRepository courseRepository) : IQueryHandler<GetCoursesQuery, IReadOnlyList<CourseListResponse>>
{
    public async ValueTask<Result<IReadOnlyList<CourseListResponse>>> Handle(GetCoursesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCenters = await adminScope.GetAdminCenterIdsAsync(cancellationToken);
        if (adminCenters.Count == 0)
            return new List<CourseListResponse>();

        var courses = await courseRepository.GetByCenterIdsAsync(adminCenters, cancellationToken);

        return courses
            .Select(c => new CourseListResponse(c.Id, c.AcademicPeriodId, c.AcademicPeriod.CenterId, c.Name, c.Code))
            .ToList();
    }
}
