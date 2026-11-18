using System.Globalization;
using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.TopicContents;

public sealed record ConversionError([property: JsonPropertyName("code")] string? Code,
                                     [property: JsonPropertyName("message")] string? Message);

public sealed record SubjectTopicConvertedCommand([property: JsonPropertyName("eventType")] string? EventType,
                                                  [property: JsonPropertyName("requestId")] Guid RequestId,
                                                  [property: JsonPropertyName("source")] ConversionSource? Source,
                                                  [property: JsonPropertyName("destinationBlobName")] string? DestinationBlobName,
                                                  [property: JsonPropertyName("contentType")] string? ContentType,
                                                  [property: JsonPropertyName("payload")] ConversionPayload? Payload,
                                                  [property: JsonPropertyName("error")] ConversionError? Error) : ICommand;

public sealed record FileUploadedUpdated([property: JsonIgnore] int UserId, int TopicId, int TopicContentId, string ProcessingState) : INotification
{
    [JsonIgnore]
    public string Type => "FileUploadedUpdated";
}

public sealed record UploadBatchCompleted([property: JsonIgnore] int UserId,
                                          int TopicId,
                                          [property: JsonIgnore] int SubjectId,
                                          [property: JsonIgnore] string TopicName,
                                          bool HasErrors) : IPersistentNotification
{
    [JsonIgnore]
    public string Type => "UploadBatchCompleted";
    public string Title => "Procesamiento de archivos finalizado";
    public string Description => $"Los archivos del tema {TopicId}-{TopicName} han terminado de procesarse.";
    string IPersistentNotification.Message => Description;
    public string Url => $"/subjects/{SubjectId}/topics/{TopicId}";
}

internal sealed class SubjectTopicConvertedHandler(ITopicRepository topics, INotificationService notifications)
    : ICommandHandler<SubjectTopicConvertedCommand>
{
    public async ValueTask<Result> Handle(SubjectTopicConvertedCommand request, CancellationToken cancellationToken)
    {
        var failed = request.EventType == "document.conversion.failed";
        if (!IsValid(request, failed))
            return Error.Validation("conversion.invalid_callback", "La notificación de conversión no es válida.");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = await HandleAttemptAsync(request, failed, cancellationToken);
            if (result.HasValue)
                return result.Value;
        }

        return Error.Unavailable("conversion.concurrency", "No se pudo confirmar la conversión; vuelve a intentarlo.");
    }

    private static bool IsValid(SubjectTopicConvertedCommand request, bool failed) =>
        (failed || request.EventType == "document.conversion.completed") && request.RequestId != Guid.Empty
        && request.Source is not null && request.Source.type == "blob" && !string.IsNullOrWhiteSpace(request.Source.blobName)
        && !string.IsNullOrWhiteSpace(request.DestinationBlobName) && !string.IsNullOrWhiteSpace(request.ContentType)
        && request.Payload is not null && request.Payload.topicContentId > 0 && request.Payload.topicId > 0 && request.Payload.userId > 0
        && (!failed || !string.IsNullOrWhiteSpace(request.Error?.Code) && !string.IsNullOrWhiteSpace(request.Error.Message));

    private async Task<Result?> HandleAttemptAsync(SubjectTopicConvertedCommand request, bool failed, CancellationToken cancellationToken)
    {
        var payload = request.Payload!;
        var topic = await topics.GetWithContentsAsync(payload.topicId, cancellationToken);
        var content = topic?.Contents.SingleOrDefault(x => x.Id == payload.topicContentId);
        if (topic is null || content is null || content.RequestId != request.RequestId)
            return Result.Success();

        if (!TryCorrelate(request, topic, content, out var destinationUrl, out var userId))
            return Error.Validation("conversion.invalid_correlation", "La notificación no coincide con la solicitud.");

        var expectedState = failed ? ContentProcessingState.Failed : ContentProcessingState.Ready;
        if (content.ProcessingState == ContentProcessingState.Pending)
        {
            ApplyResult(topic, content, request, failed, destinationUrl);
            if (!await topics.TrySaveConversionAsync(cancellationToken))
                return null;
        }
        else if (content.ProcessingState != expectedState)
        {
            return Result.Success();
        }

        await NotifyAsync(topic, content, userId, expectedState, cancellationToken);
        return Result.Success();
    }

    private static bool TryCorrelate(SubjectTopicConvertedCommand request, Topic topic, TopicContent content,
                                     out string destinationUrl, out int userId)
    {
        destinationUrl = string.Empty;
        if (!int.TryParse(content.CreatedBy, NumberStyles.None, CultureInfo.InvariantCulture, out userId)
            || userId <= 0 || userId != request.Payload!.userId || content.SourceContentUrl is null)
            return false;

        var paths = ConversionPaths.FromSource(content.SourceContentUrl, topic.SubjectId, topic.Id);
        destinationUrl = paths.DestinationUrl;
        return request.Source!.blobName == paths.Source && request.DestinationBlobName == paths.Destination
               && request.ContentType == ConversionContentType.FromFileName(content.FileName);
    }

    private static void ApplyResult(Topic topic, TopicContent content, SubjectTopicConvertedCommand request, bool failed, string destinationUrl)
    {
        if (failed)
            topic.FailContentConversion(content, request.RequestId, request.Error!.Message!);
        else
            topic.CompleteContentConversion(content, request.RequestId, destinationUrl);
    }

    private async Task NotifyAsync(Topic topic, TopicContent content, int userId, ContentProcessingState state, CancellationToken cancellationToken)
    {
        await notifications.SendAsync(new FileUploadedUpdated(userId, topic.Id, content.Id, state.ToString()), cancellationToken);
        var completion = await topics.GetContentCompletionAsync(topic.Id, cancellationToken);
        if (completion is not null)
            await notifications.SendAsync(new UploadBatchCompleted(userId, completion.TopicId,
                                                                    completion.SubjectId, completion.TopicName, completion.HasErrors), cancellationToken);
    }
}
