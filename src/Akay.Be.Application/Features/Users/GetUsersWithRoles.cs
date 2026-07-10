using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Requests;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Users;

public sealed record GetUsersWithRolesQuery : PagedQuery<List<UserWithRolesResponse>>
{
    public IReadOnlyCollection<int>? CenterIds { get; init; }
    public IReadOnlyCollection<UserRole>? Roles { get; init; }
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
}

internal sealed class GetUsersWithRolesQueryHandler(IAdminScopeService adminScope,
                                                    IUserRepository userRepository) : IQueryHandler<GetUsersWithRolesQuery, PagedResponse<List<UserWithRolesResponse>>>
{
    public async ValueTask<Result<PagedResponse<List<UserWithRolesResponse>>>> Handle(GetUsersWithRolesQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCenters = await adminScope.GetAdminCenterIdsAsync(cancellationToken);
        if (adminCenters.Count == 0)
        {
            return PagedResponse<List<UserWithRolesResponse>>.Create(new List<UserWithRolesResponse>(),
                                                                     request.PageSize,
                                                                     request.Page ?? 1,
                                                                     false);
        }

        var requestedCenterIds = request.CenterIds?.ToHashSet();
        if (requestedCenterIds is not null && requestedCenterIds.Count > 0 && !requestedCenterIds.IsSubsetOf(adminCenters))
            return Error.Forbidden("admin.forbidden", "No tienes permisos sobre algunos de los centros solicitados.");

        var filter = new UserListFilter(adminCenters,
                                        requestedCenterIds,
                                        request.Roles?.ToHashSet(),
                                        request.Search,
                                        request.IsActive);

        var pageRequest = PageRequest.From(request);

        var paged = await userRepository.GetPagedByAdminScopeAsync(filter,
                                                                   pageRequest,
                                                                   cancellationToken);

        var items = paged.Data
            .Select(u => new UserWithRolesResponse(
                u.Id,
                u.ExternalId,
                u.Email,
                u.FirstName,
                u.LastName,
                u.IsActive,
                u.RoleAssignments
                    .Where(r => r.CenterId.HasValue && adminCenters.Contains(r.CenterId.Value))
                    .Select(r => new UserCenterRoleResponse(r.CenterId!.Value, r.Role.ToString()))
                    .ToList()))
            .ToList();

        return PagedResponse<List<UserWithRolesResponse>>.Create(items,
                                                                 paged.PageSize,
                                                                 paged.Page,
                                                                 paged.HasMoreItems);
    }
}
