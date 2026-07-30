using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.AcademicPeriods;

public sealed record GetAcademicPeriodsQuery(int? CenterId = null) : IQuery<IReadOnlyList<AcademicPeriodResponse>>;

internal sealed class GetAcademicPeriodsQueryHandler(IAdminScopeService adminScope,
                                                     IUserContext userContext,
                                                     IUserRepository userRepository,
                                                     IAcademicPeriodRepository academicPeriodRepository) : IQueryHandler<GetAcademicPeriodsQuery, IReadOnlyList<AcademicPeriodResponse>>
{
    public async ValueTask<Result<IReadOnlyList<AcademicPeriodResponse>>> Handle(GetAcademicPeriodsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlySet<int> userCenters;

        if (request.CenterId.HasValue)
        {
            var hasAccess = await adminScope.EnsureAdminOrTeacherOfCenterAsync(request.CenterId.Value, cancellationToken);
            if (hasAccess.IsFailure)
                return new List<AcademicPeriodResponse>();

            userCenters = new HashSet<int> { request.CenterId.Value };
        }
        else
        {
            var currentUserId = userContext.UserId;
            if (currentUserId <= 0)
                return new List<AcademicPeriodResponse>();

            var rolesByCenter = await userRepository.GetUserRolesByCentersAsync(currentUserId, cancellationToken);
            userCenters = rolesByCenter
                .Where(kv => kv.Value.Any(r => r is UserRole.Admin or UserRole.Teacher))
                .Select(kv => kv.Key)
                .ToHashSet();

            if (userCenters.Count == 0)
                return new List<AcademicPeriodResponse>();
        }

        var periods = await academicPeriodRepository.GetByCenterIdsAsync(userCenters, cancellationToken);

        return periods
            .Select(p => new AcademicPeriodResponse(p.Id, p.CenterId, p.Name, p.StartDate, p.EndDate, p.IsActive))
            .ToList();
    }
}
