using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.To.EF.Infrastructure.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Identity;

internal static class UserSpecifications
{
    public static Specification<User> ByAdminScope(IReadOnlySet<int> centerIds,
                                                   IReadOnlySet<UserRole>? roles)
        => Specification<User>.Build(builder =>
        {
            builder.AddInclude(query => query.Include(x => x.RoleAssignments));
            builder.AddCriteria(user => user.RoleAssignments.Any(roleAssignment => roleAssignment.CenterId.HasValue
                                                                                   && centerIds.Contains(roleAssignment.CenterId.Value)
                                                                                   && (roles == null || roles.Count == 0 || roles.Contains(roleAssignment.Role))));
        });

    public static Specification<User> IsActive(bool isActive)
        => Specification<User>.Create(user => user.IsActive == isActive);

    public static Specification<User> Search(string searchPattern)
        => Specification<User>.Create(user => user.Email != null && EF.Functions.Like(user.Email, searchPattern)
                                              || user.FirstName != null && EF.Functions.Like(user.FirstName, searchPattern)
                                              || user.LastName != null && EF.Functions.Like(user.LastName, searchPattern));

}
