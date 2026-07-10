using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.EF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Academic;

internal sealed class SubjectRepository(ApplicationDbContext context) : BaseRepository<Subject, int>(context), ISubjectRepository
{
    public async Task<Subject?> GetWithCentersAsync(int id, CancellationToken cancellationToken = default)
        => await Set
            .Include(x => x.Centers)
            .Include(x => x.Admins)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Subject?> GetWithAdminsAsync(int id, CancellationToken cancellationToken = default)
        => await Set
            .Include(x => x.Admins)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<List<Subject>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set
            .Include(x => x.Centers)
            .Include(x => x.Admins)
            .ToListAsync(cancellationToken);

    public async Task<List<Subject>> GetByCenterIdsAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default)
    {
        var ids = centerIds.ToHashSet();
        return await Set
            .Include(x => x.Centers)
            .Include(x => x.Admins)
            .Where(x => x.Centers.Any(c => ids.Contains(c.CenterId)))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> SubjectIsAvailableForCenterAsync(int subjectId, int centerId, CancellationToken cancellationToken = default)
        => await context.Set<SubjectCenter>()
            .AnyAsync(x => x.SubjectId == subjectId && x.CenterId == centerId, cancellationToken);

    public async Task<bool> SubjectBelongsToAnyCenterAsync(int subjectId, IEnumerable<int> centerIds, CancellationToken cancellationToken = default)
    {
        var ids = centerIds.ToHashSet();
        return await context.Set<SubjectCenter>()
            .AnyAsync(x => x.SubjectId == subjectId && ids.Contains(x.CenterId), cancellationToken);
    }
}
