using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.EF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Academic;

internal sealed class AcademicPeriodRepository(ApplicationDbContext context) : BaseRepository<AcademicPeriod, int>(context), IAcademicPeriodRepository
{
    public async Task<List<AcademicPeriod>> GetByCenterIdAsync(int centerId, CancellationToken cancellationToken = default)
        => await Set
            .Where(x => x.CenterId == centerId)
            .ToListAsync(cancellationToken);

    public async Task<List<AcademicPeriod>> GetByCenterIdsAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default)
    {
        var ids = centerIds.ToHashSet();
        return await Set
            .Where(x => ids.Contains(x.CenterId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsInCenterAsync(int centerId, string name, int? excludingId = null, CancellationToken cancellationToken = default)
        => await Set
            .AnyAsync(x => x.CenterId == centerId
                           && x.Name == name
                           && (!excludingId.HasValue || x.Id != excludingId.Value),
                      cancellationToken);
}
