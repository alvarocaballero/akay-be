using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Persistence;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;

public interface IAcademicPeriodRepository : IBaseRepository<AcademicPeriod, int>
{
    Task<List<AcademicPeriod>> GetByCenterIdAsync(int centerId, CancellationToken cancellationToken = default);
    Task<List<AcademicPeriod>> GetByCenterIdsAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default);
    Task<bool> NameExistsInCenterAsync(int centerId, string name, int? excludingId = null, CancellationToken cancellationToken = default);
}
