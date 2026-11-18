using System.Text.Json.Serialization;
using Akay.Be.Domain.Events.Academic;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Messaging;
using Akay.To.Core.Application.Abstractions.Outbox;
using Akay.To.Core.Domain.Events;

namespace Akay.Be.Application.Features.TopicContents;

public sealed record ConversionSource([property: JsonPropertyName("type")] string type,
                                      [property: JsonPropertyName("blobName")] string blobName);

public sealed record ConversionPayload([property: JsonPropertyName("topicContentId")] int topicContentId,
                                       [property: JsonPropertyName("topicId")] int topicId,
                                       [property: JsonPropertyName("userId")] int userId);

public sealed record ConversionRequest([property: JsonPropertyName("requestId")] Guid requestId,
                                       [property: JsonPropertyName("source")] ConversionSource source,
                                       [property: JsonPropertyName("destinationBlobName")] string destinationBlobName,
                                       [property: JsonPropertyName("contentType")] string contentType,
                                       [property: JsonPropertyName("overwriteDestination")] bool overwriteDestination,
                                       [property: JsonPropertyName("callback")] string callback,
                                       [property: JsonPropertyName("payload")] ConversionPayload payload) : IOutboxDomainEvent, ICommandMessage;

internal static class ConversionPaths
{
    public static (string Source, string Destination, string DestinationUrl) FromSource(string sourceUrl, int subjectId, int topicId)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("The source URL is invalid.");
        var path = Uri.UnescapeDataString(uri.AbsolutePath);
        var prefix = $"/topic-content/subjects/{subjectId}/topics/{topicId}/sources/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException("The source URL does not belong to this topic.");
        var filename = path[prefix.Length..];
        if (filename.Contains('/') || filename.Contains('\\') || !Guid.TryParse(Path.GetFileNameWithoutExtension(filename), out _))
            throw new InvalidOperationException("The source blob name is invalid.");
        var source = path["/topic-content/".Length..];
        var destination = $"subjects/{subjectId}/topics/{topicId}/content/{Path.GetFileNameWithoutExtension(filename)}.md";
        var destinationUri = new UriBuilder(uri) { Path = $"/topic-content/{destination}", Query = "", Fragment = "" }.Uri.AbsoluteUri;
        return (source, destination, destinationUri);
    }
}

internal sealed class TopicContentUploadCompletedHandler(IOutboxEventWriter writer) : IDomainEventHandler<TopicContentUploadCompleted>
{
    public ValueTask Handle(TopicContentUploadCompleted domainEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var paths = ConversionPaths.FromSource(domainEvent.SourceUrl, domainEvent.SubjectId, domainEvent.TopicId);
        var type = ConversionContentType.FromFileName(domainEvent.FileName)
                   ?? throw new InvalidOperationException("Unsupported conversion content type.");
        writer.Enqueue(new ConversionRequest(domainEvent.RequestId, new ConversionSource("blob", paths.Source),
                                             paths.Destination, type, true, "topic-content",
                                             new ConversionPayload(domainEvent.ContentId, domainEvent.TopicId, domainEvent.UserId)));
        return ValueTask.CompletedTask;
    }
}
