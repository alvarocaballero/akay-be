using Akay.Be.Domain.Enums;
using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class TopicContent : Entity<int>, IAuditable, ISoftDeletable
{
    private TopicContent() { }

    public int TopicId { get; private set; }
    public Topic Topic { get; private set; } = default!;
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public string? OriginalContentUrl { get; private set; }
    public string? ContentUrl { get; private set; }
    public ContentProcessingState ProcessingState { get; private set; }
    public ContentIndexingState IndexingState { get; private set; }
    public string? ProcessingError { get; private set; }
    public string? IndexingError { get; private set; }
    public int ProcessingAttemptId { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    internal static TopicContent Create(int topicId,
                                        string fileName,
                                        string contentType,
                                        string? originalContentUrl,
                                        string? contentUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        return new TopicContent
        {
            TopicId = topicId,
            FileName = fileName,
            ContentType = contentType,
            OriginalContentUrl = originalContentUrl,
            ContentUrl = contentUrl,
            ProcessingState = ContentProcessingState.UploadPending,
            IndexingState = ContentIndexingState.NotIndexed,
            ProcessingAttemptId = 0
        };
    }

}
