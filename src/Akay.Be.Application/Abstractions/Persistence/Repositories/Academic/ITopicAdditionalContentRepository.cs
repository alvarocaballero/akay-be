using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Persistence;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;

public interface ITopicAdditionalContentRepository : IReadBaseRepository<TopicAdditionalContent, int>
{
    Task<List<TopicAdditionalContent>> GetByTopicIdAsync(int topicId, CancellationToken cancellationToken = default);
}
