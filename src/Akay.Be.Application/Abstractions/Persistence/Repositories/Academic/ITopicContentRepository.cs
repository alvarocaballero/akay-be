using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Persistence;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;

public interface ITopicContentRepository : IReadBaseRepository<TopicContent, int>
{
    Task<List<TopicContent>> GetByTopicIdAsync(int topicId, CancellationToken cancellationToken = default);
}
