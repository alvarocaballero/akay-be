using Akay.To.Core.Domain.Auditing;
using Akay.To.Core.Domain.Entities;

namespace Akay.Be.Domain.Entities.Academic;

public sealed class TopicAdditionalContent : Entity<int>, IAuditable, ISoftDeletable
{
    private TopicAdditionalContent() { }

    public int TopicId { get; private set; }
    public Topic Topic { get; private set; } = default!;
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public string? SourceContentUrl { get; private set; }
    public string? ContentUrl { get; private set; }
    public bool? IsVisible { get; private set; }
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

    internal static TopicAdditionalContent Create(int topicId,
                                                  string fileName,
                                                  string contentType,
                                                  string? sourceContentUrl,
                                                  string? contentUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        return new TopicAdditionalContent
        {
            TopicId = topicId,
            FileName = fileName,
            ContentType = contentType,
            SourceContentUrl = sourceContentUrl,
            ContentUrl = contentUrl
        };
    }
}
