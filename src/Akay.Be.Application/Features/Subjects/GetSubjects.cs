using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Subjects;

public sealed record GetSubjectsQuery : IQuery<IReadOnlyList<SubjectResponse>>
{
    public int? CenterId { get; init; }
}

internal sealed class GetSubjectsQueryHandler(IAdminScopeService adminScope,
                                              ISubjectRepository subjectRepository) : IQueryHandler<GetSubjectsQuery, IReadOnlyList<SubjectResponse>>
{
    public async ValueTask<Result<IReadOnlyList<SubjectResponse>>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlySet<int> targetCenters;

        if (request.CenterId.HasValue)
        {
            var adminCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId.Value, cancellationToken);
            if (adminCheck.IsFailure)
                return adminCheck.Error;

            targetCenters = new HashSet<int> { request.CenterId.Value };
        }
        else
        {
            var adminCenters = await adminScope.GetAdminCenterIdsAsync(cancellationToken);
            if (adminCenters.Count == 0)
                return new List<SubjectResponse>();

            targetCenters = adminCenters;
        }

        var subjects = await subjectRepository.GetByCenterIdsAsync(targetCenters, cancellationToken);

        return subjects
            .Select(s => new SubjectResponse(
                s.Id,
                s.Name,
                s.Description,
                s.Centers.Select(c => c.CenterId).ToList()))
            .ToList();
    }
}
