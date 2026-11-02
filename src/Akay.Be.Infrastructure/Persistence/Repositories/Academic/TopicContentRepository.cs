using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.EF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Academic;

internal sealed class TopicContentRepository(ApplicationDbContext context) : BaseRepository<TopicContent, int>(context), ITopicContentRepository
{
    public async Task<List<TopicContent>> GetByTopicIdAsync(int topicId, CancellationToken cancellationToken = default)
        => await Set
            .Where(x => x.TopicId == topicId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
}
