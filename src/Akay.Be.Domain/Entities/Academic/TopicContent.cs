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
    public DateTimeOffset? UploadStartedAt { get; private set; }
    public int? UploadUserId { get; private set; }
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
            ProcessingState = ContentProcessingState.Uploading,
            IndexingState = ContentIndexingState.NotIndexed,
            ProcessingAttemptId = 1
        };
    }

    public void StartUpload(string fileName, string contentType, int userId, DateTimeOffset startedAt)
    {
        if (ProcessingState != ContentProcessingState.UploadFailed)
            throw new InvalidOperationException("Only failed uploads can be retried.");

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        FileName = fileName;
        ContentType = contentType;
        ProcessingError = null;
        ProcessingAttemptId = checked(ProcessingAttemptId + 1);
        ProcessingState = ContentProcessingState.Uploading;
        UploadStartedAt = startedAt;
        UploadUserId = userId;
    }

    public void SetInitialUploadDetails(int userId, DateTimeOffset startedAt)
    {
        EnsureUploading();
        if (UploadStartedAt.HasValue)
            throw new InvalidOperationException("Upload details have already been set.");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        UploadStartedAt = startedAt;
        UploadUserId = userId;
    }

    public void CompleteUpload(string originalContentUrl)
    {
        EnsureUploading();
        ArgumentException.ThrowIfNullOrWhiteSpace(originalContentUrl);
        OriginalContentUrl = originalContentUrl;
        ProcessingError = null;
        ProcessingState = ContentProcessingState.Pending;
    }

    public void FailUpload(string error)
    {
        EnsureUploading();
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        ProcessingError = error;
        ProcessingState = ContentProcessingState.UploadFailed;
    }

    public void CompleteIndexed(string contentUrl)
    {
        EnsureIndexing();
        ArgumentException.ThrowIfNullOrWhiteSpace(contentUrl);
        ContentUrl = contentUrl;
        IndexingError = null;
        IndexingState = ContentIndexingState.Indexed;
    }

    public void FailIndexing(string error)
    {
        EnsureIndexing();
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        IndexingError = error;
        IndexingState = ContentIndexingState.NotIndexed;
    }

    private void EnsureUploading()
    {
        if (ProcessingState != ContentProcessingState.Uploading)
            throw new InvalidOperationException("The content is not uploading.");
    }

    private void EnsureIndexing()
    {
        if (IndexingState != ContentIndexingState.Indexing)
            throw new InvalidOperationException("The content is not indexing.");
    }
}
