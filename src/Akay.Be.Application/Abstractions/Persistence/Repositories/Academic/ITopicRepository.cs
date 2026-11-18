using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Persistence;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;

public interface ITopicRepository : IBaseRepository<Topic, int>
{
    Task<List<Topic>> GetBySubjectIdAsync(int subjectId, CancellationToken cancellationToken = default);
    Task<int> GetMaxDisplayOrderAsync(int subjectId, CancellationToken cancellationToken = default);
    Task<Topic?> GetWithContentsAsync(int topicId, CancellationToken cancellationToken = default);
    Task<Topic?> GetWithAdditionalContentsAsync(int topicId, CancellationToken cancellationToken = default);
    TopicContent AddContent(Topic topic, string fileName, string contentType,
                            string? sourceContentUrl = null, string? contentUrl = null);
    TopicAdditionalContent AddAdditionalContent(Topic topic, string fileName, string contentType,
                                                string? sourceContentUrl = null, string? contentUrl = null);
    void RemoveContent(Topic topic, TopicContent content);
    void RemoveAdditionalContent(Topic topic, TopicAdditionalContent content);
    Task<bool> TrySaveConversionAsync(CancellationToken cancellationToken = default);
    Task<TopicContentCompletion?> GetContentCompletionAsync(int topicId, CancellationToken cancellationToken = default);
}

public sealed record TopicContentCompletion(int SubjectId, int TopicId, string TopicName, bool HasErrors);
