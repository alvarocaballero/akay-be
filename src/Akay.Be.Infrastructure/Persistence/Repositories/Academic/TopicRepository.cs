using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.EF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Academic;

internal sealed class TopicRepository(ApplicationDbContext context) : BaseRepository<Topic, int>(context), ITopicRepository
{
    public async Task<List<Topic>> GetBySubjectIdAsync(int subjectId, CancellationToken cancellationToken = default)
        => await Set
            .Where(x => x.SubjectId == subjectId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<int> GetMaxDisplayOrderAsync(int subjectId, CancellationToken cancellationToken = default)
        => await Set
            .Where(x => x.SubjectId == subjectId)
            .Select(x => (int?)x.DisplayOrder)
            .MaxAsync(cancellationToken) ?? 0;
}
