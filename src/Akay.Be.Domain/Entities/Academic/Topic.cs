using Akay.Be.Domain.Enums;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class Topic : AggregateRoot<int>, IAuditable, ISoftDeletable
{
    private readonly List<TopicContent> _contents = [];

    private Topic() { }

    public int SubjectId { get; private set; }
    public Subject Subject { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public TopicProcessingState ProcessingState { get; private set; }
    public TopicIndexingState IndexingState { get; private set; }
    public bool IsActive { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144
    public IReadOnlyCollection<TopicContent> Contents => _contents.AsReadOnly();

    public static Topic Create(int subjectId, string name, string? description = null)
    {
        if (subjectId <= 0)
            throw new ArgumentException("SubjectId must be greater than zero.", nameof(subjectId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Topic
        {
            SubjectId = subjectId,
            Name = name,
            Description = description,
            ProcessingState = TopicProcessingState.Draft,
            IndexingState = TopicIndexingState.NotIndexed,
            IsActive = false
        };
    }

    public TopicContent AddContent(string fileName,
                                   string contentType,
                                   string? originalContentUrl = null,
                                   string? contentUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var content = TopicContent.Create(Id,
                                          fileName,
                                          contentType,
                                          originalContentUrl,
                                          contentUrl);

        _contents.Add(content);

        return content;
    }

    public void DeleteContent(TopicContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!_contents.Remove(content))
            throw new InvalidOperationException("Content does not belong to this topic.");
    }

    public void ChangeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Name == name)
            return;

        Name = name;
    }

    public void ChangeDescription(string? description)
    {
        Description = description;
    }

}
