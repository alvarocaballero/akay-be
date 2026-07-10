using Akay.Be.Application.Features.Users;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Requests;
using Akay.To.Core.Application.Responses;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;

public interface IUserRepository : IBaseRepository<User, int>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<List<User>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> UserExistsWithRoleAsync(int userId, int? centerId, UserRole role, CancellationToken cancellationToken = default);
    Task<bool> UserHasActiveRoleInCenterAsync(int userId, int centerId, UserRole role, CancellationToken cancellationToken = default);
    Task<Dictionary<int, List<UserRole>>> GetUserRolesByCentersAsync(int userId, CancellationToken cancellationToken = default);

    Task<List<User>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);

    Task<PagedResponse<List<User>>> GetPagedByAdminScopeAsync(
        UserListFilter filter,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);
}
