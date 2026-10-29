using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Persistence;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;

public interface ITopicRepository : IBaseRepository<Topic, int>
{
    Task<List<Topic>> GetBySubjectIdAsync(int subjectId, CancellationToken cancellationToken = default);
    Task<int> GetMaxDisplayOrderAsync(int subjectId, CancellationToken cancellationToken = default);
}
