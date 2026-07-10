using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Features.Users;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.Core.Application.Requests;
using Akay.To.Core.Application.Responses;
using Akay.To.EF.Infrastructure.Queries;
using Akay.To.EF.Infrastructure.Repositories;
using Akay.To.EF.Infrastructure.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Identity;

internal sealed class UserRepository(ApplicationDbContext context) : BaseRepository<User, int>(context), IUserRepository
{

    public override async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await Set
            .Include(x => x.RoleAssignments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => await Set
            .Include(x => x.RoleAssignments)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

    public async Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set
            .Include(x => x.RoleAssignments)
            .ToListAsync(cancellationToken);

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(x => x.Email == email, cancellationToken);

    public async Task<List<User>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
        => await Set
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

    public async Task<bool> UserExistsWithRoleAsync(int userId, int? centerId, UserRole role, CancellationToken cancellationToken = default)
        => await context.Set<UserRoleAssignment>()
            .AnyAsync(x => x.UserId == userId && x.CenterId == centerId && x.Role == role, cancellationToken);

    public async Task<bool> UserHasActiveRoleInCenterAsync(int userId, int centerId, UserRole role, CancellationToken cancellationToken = default)
        => await context.Set<UserRoleAssignment>()
            .AnyAsync(x => x.UserId == userId && x.CenterId == centerId && x.Role == role, cancellationToken);

    public async Task<Dictionary<int, List<UserRole>>> GetUserRolesByCentersAsync(int userId, CancellationToken cancellationToken = default)
    {
        var assignments = await context.Set<UserRoleAssignment>()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        return assignments
            .Where(x => x.CenterId.HasValue)
            .GroupBy(x => x.CenterId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Role).ToList());
    }

    public async Task<PagedResponse<List<User>>> GetPagedByAdminScopeAsync(UserListFilter filter,
                                                                           PageRequest pageRequest,
                                                                           CancellationToken cancellationToken = default)
    {
        var effectiveCenterIds = filter.CenterIds is null || filter.CenterIds.Count == 0
            ? filter.AdminCenterIds
            : filter.CenterIds.Intersect(filter.AdminCenterIds).ToHashSet();

        var query = Set.ApplySpecification(UserSpecifications.ByAdminScope(effectiveCenterIds, filter.Roles));

        if (filter.IsActive.HasValue)
        {
            query = query.ApplySpecification(UserSpecifications.IsActive(filter.IsActive.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.ApplySpecification(UserSpecifications.Search(SqlHelpers.Contains(filter.Search.Trim())));
        }

        var sortMap = new Dictionary<string, Func<IQueryable<User>, bool, IOrderedQueryable<User>>>
        {
            ["email"] = (q, asc) => asc ? q.OrderBy(x => x.Email) : q.OrderByDescending(x => x.Email),
            ["firstName"] = (q, asc) => asc ? q.OrderBy(x => x.FirstName) : q.OrderByDescending(x => x.FirstName),
            ["lastName"] = (q, asc) => asc ? q.OrderBy(x => x.LastName) : q.OrderByDescending(x => x.LastName),
            ["isActive"] = (q, asc) => asc ? q.OrderBy(x => x.IsActive) : q.OrderByDescending(x => x.IsActive)
        };


        return await query
            .ApplyOrdering(pageRequest.SortBy, pageRequest.IsAscending, sortMap, (q, asc) => asc ? q.OrderBy(x => x.Id) : q.OrderByDescending(x => x.Id))
            .ToPagedResponseAsync(pageRequest.Page, pageRequest.PageSize, cancellationToken: cancellationToken);
    }
}
