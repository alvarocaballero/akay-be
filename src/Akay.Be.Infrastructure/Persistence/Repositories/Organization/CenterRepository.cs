using Akay.Be.Application.Abstractions.Persistence.Repositories.Organization;
using Akay.Be.Domain.Entities.Organization;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.EF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Organization;

internal sealed class CenterRepository(ApplicationDbContext context) : BaseRepository<Center, int>(context), ICenterRepository
{
    public async Task<List<Center>> GetAllAsync(CancellationToken cancellationToken = default)
        => await Set.ToListAsync(cancellationToken);

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
        => await Set.AnyAsync(x => x.Code == code, cancellationToken);
}
