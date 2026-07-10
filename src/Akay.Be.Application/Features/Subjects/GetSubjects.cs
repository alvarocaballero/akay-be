using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Subjects;

public sealed record GetSubjectsQuery : IQuery<IReadOnlyList<SubjectResponse>>;

internal sealed class GetSubjectsQueryHandler(IAdminScopeService adminScope,
                                              ISubjectRepository subjectRepository) : IQueryHandler<GetSubjectsQuery, IReadOnlyList<SubjectResponse>>
{
    public async ValueTask<Result<IReadOnlyList<SubjectResponse>>> Handle(GetSubjectsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCenters = await adminScope.GetAdminCenterIdsAsync(cancellationToken);
        if (adminCenters.Count == 0)
            return new List<SubjectResponse>();

        var subjects = await subjectRepository.GetByCenterIdsAsync(adminCenters, cancellationToken);

        return subjects
            .Select(s => new SubjectResponse(
                s.Id,
                s.Name,
                s.Description,
                s.Centers.Select(c => c.CenterId).ToList()))
            .ToList();
    }
}
