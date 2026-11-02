using Akay.Be.Domain.Enums;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class Topic : AggregateRoot<int>, IAuditable, ISoftDeletable
{
    private readonly List<TopicContent> _contents = [];
    private readonly List<TopicAdditionalContent> _additionalContents = [];

    private Topic() { }

    public int SubjectId { get; private set; }
    public Subject Subject { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public TopicProcessingState ProcessingState { get; private set; }
    public TopicIndexingState IndexingState { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144
    public IReadOnlyCollection<TopicContent> Contents => _contents.AsReadOnly();
    public IReadOnlyCollection<TopicAdditionalContent> AdditionalContents => _additionalContents.AsReadOnly();

    public static Topic Create(int subjectId, string name, string? description = null, int displayOrder = 1)
    {
        if (subjectId <= 0)
            throw new ArgumentException("SubjectId must be greater than zero.", nameof(subjectId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (displayOrder <= 0)
            throw new ArgumentException("DisplayOrder must be greater than zero.", nameof(displayOrder));

        return new Topic
        {
            SubjectId = subjectId,
            Name = name,
            Description = description,
            ProcessingState = TopicProcessingState.Draft,
            IndexingState = TopicIndexingState.NotIndexed,
            IsActive = false,
            DisplayOrder = displayOrder
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

    public TopicAdditionalContent AddAdditionalContent(string fileName,
                                                       string contentType,
                                                       string? sourceContentUrl = null,
                                                       string? contentUrl = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var additionalContent = TopicAdditionalContent.Create(Id,
                                                              fileName,
                                                              contentType,
                                                              sourceContentUrl,
                                                              contentUrl);

        _additionalContents.Add(additionalContent);

        return additionalContent;
    }

    public void DeleteAdditionalContent(TopicAdditionalContent additionalContent)
    {
        ArgumentNullException.ThrowIfNull(additionalContent);

        if (!_additionalContents.Remove(additionalContent))
            throw new InvalidOperationException("Additional content does not belong to this topic.");
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

    public void ChangeDisplayOrder(int displayOrder)
    {
        if (displayOrder <= 0)
            throw new ArgumentException("DisplayOrder must be greater than zero.", nameof(displayOrder));

        DisplayOrder = displayOrder;
    }

    public void ChangeProcessingState(TopicProcessingState processingState)
    {
        if (!Enum.IsDefined(processingState))
            throw new ArgumentException("Invalid processing state.", nameof(processingState));

        ProcessingState = processingState;
    }

    public void ChangeIndexingState(TopicIndexingState indexingState)
    {
        if (!Enum.IsDefined(indexingState))
            throw new ArgumentException("Invalid indexing state.", nameof(indexingState));

        IndexingState = indexingState;
    }

    public void ChangeActivation(bool isActive)
    {
        IsActive = isActive;
    }
}
