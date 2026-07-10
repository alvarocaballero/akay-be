using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Requests;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Students;

public sealed record GetStudentsQuery : PagedQuery<List<StudentResponse>>
{
    public IReadOnlyCollection<int>? CenterIds { get; init; }
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
}

internal sealed class GetStudentsQueryHandler(IAdminScopeService adminScope,
                                              IStudentRepository studentRepository) : IQueryHandler<GetStudentsQuery, PagedResponse<List<StudentResponse>>>
{
    public async ValueTask<Result<PagedResponse<List<StudentResponse>>>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCenters = await adminScope.GetAdminCenterIdsAsync(cancellationToken);
        if (adminCenters.Count == 0)
        {
            return PagedResponse<List<StudentResponse>>.Create([],
                                                               request.PageSize,
                                                               request.Page ?? 1,
                                                               false);
        }

        var requestedCenterIds = request.CenterIds?.ToHashSet();
        if (requestedCenterIds is not null && requestedCenterIds.Count > 0 && !requestedCenterIds.IsSubsetOf(adminCenters))
            return Error.Forbidden("admin.forbidden", "No tienes permisos sobre algunos de los centros solicitados.");

        var filter = new StudentListFilter(adminCenters,
                                           requestedCenterIds,
                                           request.Search,
                                           request.IsActive);

        var paged = await studentRepository.GetPagedByAdminScopeAsync(filter,
                                                                       PageRequest.From(request),
                                                                       cancellationToken);

        return paged;
    }

}
