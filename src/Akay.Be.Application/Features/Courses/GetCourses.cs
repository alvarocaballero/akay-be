using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Courses;

public sealed record GetCoursesQuery(int CenterId, int? AcademicPeriodId = null) : IQuery<IReadOnlyList<CourseListResponse>>;

internal sealed class GetCoursesQueryHandler(IAdminScopeService adminScope,
                                             ICourseRepository courseRepository) : IQueryHandler<GetCoursesQuery, IReadOnlyList<CourseListResponse>>
{
    public async ValueTask<Result<IReadOnlyList<CourseListResponse>>> Handle(GetCoursesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (adminCheck.IsFailure)
            return adminCheck.Error;

        var courses = await courseRepository.GetByCenterIdsAsync([request.CenterId], cancellationToken);

        if (request.AcademicPeriodId.HasValue)
            courses = courses.Where(c => c.AcademicPeriodId == request.AcademicPeriodId.Value).ToList();

        return courses
            .Select(c => new CourseListResponse(c.Id, c.AcademicPeriodId, c.AcademicPeriod.CenterId, c.Name, c.Code))
            .ToList();
    }
}
