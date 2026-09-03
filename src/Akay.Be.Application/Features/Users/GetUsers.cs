using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Requests;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Users;

public sealed record GetUsersQuery : PagedQuery<List<UserWithRolesResponse>>
{
    public IReadOnlyCollection<int>? CenterIds { get; init; }
    public IReadOnlyCollection<UserRole>? Roles { get; init; }
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
}

internal sealed class GetUsersQueryHandler(IAdminScopeService adminScope,
                                           IUserRepository userRepository) : IQueryHandler<GetUsersQuery, PagedResponse<List<UserWithRolesResponse>>>
{
    public async ValueTask<Result<PagedResponse<List<UserWithRolesResponse>>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCenters = await adminScope.GetAdminCenterIdsAsync(cancellationToken);
        if (adminCenters.Count == 0)
            return PagedResponse<List<UserWithRolesResponse>>.Create();

        var requestedCenterIds = request.CenterIds?.ToHashSet();
        if (requestedCenterIds is not null && requestedCenterIds.Count > 0 && !requestedCenterIds.IsSubsetOf(adminCenters))
            return Error.Forbidden("admin.forbidden", "No tienes permisos sobre algunos de los centros solicitados.");

        var filter = new UserListFilter(adminCenters,
                                        requestedCenterIds,
                                        request.Roles?.ToHashSet(),
                                        request.Search,
                                        request.IsActive);

        var paged = await userRepository.GetPagedByAdminScopeAsync(filter, PageRequest.From(request), cancellationToken);

        var items = paged.Data
            .Select(u => new UserWithRolesResponse(
                u.Id,
                u.ExternalId,
                u.Email,
                u.FirstName,
                u.LastName,
                u.IsActive,
                u.RoleAssignments
                    .Where(r => r.CenterId.HasValue && r.DeletedAt == null && adminCenters.Contains(r.CenterId.Value))
                    .Select(r => new UserCenterRoleResponse(r.CenterId!.Value, r.Role.ToString()))
                    .ToList()))
            .ToList();

        return PagedResponse<List<UserWithRolesResponse>>.Create(items,
                                                                paged.PageSize,
                                                                paged.Page,
                                                                paged.HasMoreItems);
    }
}
