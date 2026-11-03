using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.BlobStorage;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using Microsoft.Extensions.Logging;

namespace Akay.Be.Application.Features.TopicContents;

public sealed record UploadTopicContentCommand(int TopicId, int? ContentId, string FileName, string ContentType, long Length, Stream Content) : ICommand<TopicContentResponse>;

public sealed record TopicContentStateChanged(int UserId, int TopicId, int TopicContentId, ContentProcessingState State) : INotification
{
    public string Type => "topicContent.stateChanged";
}

#pragma warning disable S107
internal sealed class UploadTopicContentCommandHandler(IAdminScopeService adminScope,
                                                       ITopicRepository topics,
                                                       ITopicContentRepository contents,
                                                       IUnitOfWork unitOfWork,
                                                       IBlobStorageServiceFactory storageFactory,
                                                       INotificationService notifications,
                                                       IUserContext userContext,
                                                       ILogger<UploadTopicContentCommandHandler> logger) : ICommandHandler<UploadTopicContentCommand, TopicContentResponse>
#pragma warning restore S107
{
    private const long MaximumBytes = 20 * 1024 * 1024;
    private const int UploadTimeoutSeconds = 120;

    public async ValueTask<Result<TopicContentResponse>> Handle(UploadTopicContentCommand request, CancellationToken cancellationToken)
    {
        if (request.Length <= 0 || request.Length > MaximumBytes || string.IsNullOrWhiteSpace(request.FileName)
            || request.FileName.Length > 500 || string.IsNullOrWhiteSpace(request.ContentType) || request.ContentType.Length > 255)
            return Error.Validation("topic_content.invalid_file", "El archivo debe tener un nombre y tipo válidos y un tamaño entre 1 byte y 20 MB.");

        var topic = await topics.GetByIdAsync(request.TopicId, cancellationToken);
        if (topic is null)
            return Error.NotFound("topic.not_found", $"Tema {request.TopicId} no encontrado.");

        var access = await adminScope.EnsureCanWriteSubjectContentAsync(topic.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        if (userContext.UserId <= 0)
            return Error.Forbidden("topic_content.invalid_user", "Usuario no válido.");

        var content = request.ContentId.HasValue ? await contents.GetByIdAsync(request.ContentId.Value, cancellationToken) : null;

        if (request.ContentId.HasValue && (content is null || content.TopicId != topic.Id))
            return Error.NotFound("topic_content.not_found", "Contenido no encontrado.");

        if (content is not null && content.ProcessingState != ContentProcessingState.UploadFailed)
            return Error.Conflict("topic_content.not_retryable", "Solo se pueden reintentar subidas fallidas.");

        if (content is null)
        {
            content = topic.AddContent(request.FileName, request.ContentType);
            content.SetInitialUploadDetails(userContext.UserId, DateTimeOffset.UtcNow);
            contents.Add(content);
        }
        else
        {
            content.StartUpload(request.FileName, request.ContentType, userContext.UserId, DateTimeOffset.UtcNow);
        }

        var saved = await unitOfWork.ResultSaveChangesAsync(cancellationToken);
        if (saved.IsFailure)
            return saved.Error;

        await NotifyAsync(content.Id, content.TopicId, content.ProcessingState);

        string uri;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(UploadTimeoutSeconds));
            var storage = await storageFactory.CreateAsync("topic-content", cancellationToken: timeout.Token);
            uri = await storage.UploadAsync($"topics/{topic.Id}/contents/{content.Id}/{content.ProcessingAttemptId}", request.Content, request.ContentType,
                                            overwrite: false, cancellationToken: timeout.Token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Upload failed for topic content {TopicContentId}", content.Id);
            content.FailUpload("La subida ha fallado. Vuelve a intentarlo.");

            using var saveTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var failed = await unitOfWork.ResultSaveChangesAsync(saveTimeout.Token);
            if (failed.IsFailure)
                return failed.Error;

            await NotifyAsync(content.Id, content.TopicId, content.ProcessingState);
            return Error.Internal("topic_content.upload_failed", "La subida ha fallado. Vuelve a intentarlo.");
        }

        content.CompleteUpload(uri);
        var completed = await unitOfWork.ResultSaveChangesAsync(cancellationToken);
        if (completed.IsFailure)
            return completed.Error;

        await NotifyAsync(content.Id, content.TopicId, content.ProcessingState);

        return new TopicContentResponse(content.Id, content.TopicId, content.FileName, content.ContentType,
                                        content.OriginalContentUrl, content.ContentUrl, content.ProcessingState,
                                        content.IndexingState, content.ProcessingError, content.IndexingError, content.ProcessingAttemptId);
    }

    private async Task NotifyAsync(int contentId, int topicId, ContentProcessingState state)
    {
        try
        {
            await notifications.SendAsync(new TopicContentStateChanged(userContext.UserId, topicId, contentId, state), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not notify state change for topic content {TopicContentId}", contentId);
        }
    }
}
