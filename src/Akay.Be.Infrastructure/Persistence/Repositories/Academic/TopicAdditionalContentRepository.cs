using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.EF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Academic;

internal sealed class TopicAdditionalContentRepository(ApplicationDbContext context) : BaseRepository<TopicAdditionalContent, int>(context), ITopicAdditionalContentRepository
{
    public async Task<List<TopicAdditionalContent>> GetByTopicIdAsync(int topicId, CancellationToken cancellationToken = default)
        => await Set
            .Where(x => x.TopicId == topicId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
