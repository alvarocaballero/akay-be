using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Requests;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Students;

public sealed record GetStudentsRequest : PagedQuery<List<StudentResponse>>
{
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record GetStudentsQuery(int CenterId, string? Search, bool? IsActive) : PagedQuery<List<StudentResponse>>;

internal sealed class GetStudentsQueryHandler(IAdminScopeService adminScope,
                                              IStudentRepository studentRepository) : IQueryHandler<GetStudentsQuery, PagedResponse<List<StudentResponse>>>
{
    public async ValueTask<Result<PagedResponse<List<StudentResponse>>>> Handle(GetStudentsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureAdminOrTeacherOfCenterAsync(request.CenterId, cancellationToken);
        if (access.IsFailure)
            return PagedResponse<List<StudentResponse>>.Create([],
                                                               request.PageSize,
                                                               request.Page ?? 1,
                                                               false);

        var filter = new StudentListFilter(new HashSet<int> { request.CenterId },
                                           request.Search,
                                           request.IsActive);

        var paged = await studentRepository.GetPagedByAdminScopeAsync(filter,
                                                                       PageRequest.From(request),
                                                                       cancellationToken);

        return paged;
    }

}
