using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.AcademicPeriods;

public sealed record GetAcademicPeriodsQuery : IQuery<IReadOnlyList<AcademicPeriodResponse>>;

internal sealed class GetAcademicPeriodsQueryHandler(IAdminScopeService adminScope,
                                                     IAcademicPeriodRepository academicPeriodRepository) : IQueryHandler<GetAcademicPeriodsQuery, IReadOnlyList<AcademicPeriodResponse>>
{
    public async ValueTask<Result<IReadOnlyList<AcademicPeriodResponse>>> Handle(GetAcademicPeriodsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCenters = await adminScope.GetAdminCenterIdsAsync(cancellationToken);
        if (adminCenters.Count == 0)
            return new List<AcademicPeriodResponse>();

        var periods = await academicPeriodRepository.GetByCenterIdsAsync(adminCenters, cancellationToken);

        return periods
            .Select(p => new AcademicPeriodResponse(p.Id, p.CenterId, p.Name, p.StartDate, p.EndDate, p.IsActive))
            .ToList();
    }
}
