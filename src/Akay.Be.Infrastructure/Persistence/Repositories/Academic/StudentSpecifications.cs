using Akay.Be.Domain.Entities.Academic;
using Akay.To.EF.Infrastructure.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Academic;

internal static class StudentSpecifications
{
    public static Specification<Student> ByCenterIds(IReadOnlySet<int> centerIds)
        => Specification<Student>.Create(student => centerIds.Contains(student.CenterId));

    public static Specification<Student> IsActive(bool isActive)
        => Specification<Student>.Create(student => student.IsActive == isActive);

    public static Specification<Student> NumberSearch(string searchPattern)
        => Specification<Student>.Create(student => student.StudentNumber != null
                                                    && EF.Functions.Like(student.StudentNumber, searchPattern));
}
