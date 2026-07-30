using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.AcademicPeriods;

public sealed record GetAcademicPeriodByIdQuery(int Id) : IQuery<AcademicPeriodResponse>;

internal sealed class GetAcademicPeriodByIdQueryHandler(IAdminScopeService adminScope,
                                                        IAcademicPeriodRepository academicPeriodRepository) : IQueryHandler<GetAcademicPeriodByIdQuery, AcademicPeriodResponse>
{
    public async ValueTask<Result<AcademicPeriodResponse>> Handle(GetAcademicPeriodByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var period = await academicPeriodRepository.GetByIdAsync(request.Id, cancellationToken);
        if (period is null || period.DeletedAt is not null)
            return Error.NotFound("academicperiod.not_found", $"Periodo académico {request.Id} no encontrado.");

        var hasAccess = await adminScope.EnsureAdminOrTeacherOfCenterAsync(period.CenterId, cancellationToken);
        if (hasAccess.IsFailure)
            return Error.Forbidden("academicperiod.forbidden", $"No tienes permisos sobre el periodo académico {request.Id}.");

        return new AcademicPeriodResponse(period.Id, period.CenterId, period.Name, period.StartDate, period.EndDate, period.IsActive);
    }
}
