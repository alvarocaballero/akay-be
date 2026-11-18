using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.EF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Academic;

internal sealed class TopicRepository(ApplicationDbContext context) : BaseRepository<Topic, int>(context), ITopicRepository
{
    public Task<Topic?> GetWithContentsAsync(int topicId, CancellationToken cancellationToken = default)
        => Set.Include(x => x.Contents).SingleOrDefaultAsync(x => x.Id == topicId, cancellationToken);

    public Task<Topic?> GetWithAdditionalContentsAsync(int topicId, CancellationToken cancellationToken = default)
        => Set.Include(x => x.AdditionalContents).SingleOrDefaultAsync(x => x.Id == topicId, cancellationToken);

    public TopicContent AddContent(Topic topic, string fileName, string contentType,
                                   string? sourceContentUrl = null, string? contentUrl = null)
    {
        var content = topic.AddContent(fileName, contentType, sourceContentUrl, contentUrl);
        context.TopicContents.Add(content);
        return content;
    }

    public TopicAdditionalContent AddAdditionalContent(Topic topic, string fileName, string contentType,
                                                       string? sourceContentUrl = null, string? contentUrl = null)
    {
        var content = topic.AddAdditionalContent(fileName, contentType, sourceContentUrl, contentUrl);
        context.TopicAdditionalContents.Add(content);
        return content;
    }

    public void RemoveContent(Topic topic, TopicContent content)
    {
        topic.DeleteContent(content);
        context.TopicContents.Remove(content);
    }

    public void RemoveAdditionalContent(Topic topic, TopicAdditionalContent content)
    {
        topic.DeleteAdditionalContent(content);
        context.TopicAdditionalContents.Remove(content);
    }

    public async Task<bool> TrySaveConversionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<TopicContentCompletion?> GetContentCompletionAsync(int topicId, CancellationToken cancellationToken = default)
    {
        var topic = await Set.AsNoTracking()
            .Where(x => x.Id == topicId)
            .Select(x => new { x.Id, x.SubjectId, x.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (topic is null)
            return null;

        var states = await context.TopicContents.AsNoTracking().Where(x => x.TopicId == topicId)
            .Select(x => x.ProcessingState).ToListAsync(cancellationToken);
        return states.Count > 0 && states.All(x => x is ContentProcessingState.Ready or ContentProcessingState.Failed)
            ? new TopicContentCompletion(topic.SubjectId, topic.Id, topic.Name, states.Contains(ContentProcessingState.Failed))
            : null;
    }

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
