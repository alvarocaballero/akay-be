using System.Diagnostics.CodeAnalysis;
using Akay.Be.Application.Features.LearningHubs.BlobStorageExamples;
using Akay.Be.Application.Features.LearningHubs.CognitiveServicesExamples;
using Akay.Be.Application.Features.LearningHubs.HttpExamples;
using Akay.Be.Application.Features.LearningHubs.MediatorExamples;
using Akay.Be.Application.Features.LearningHubs.Messaging;
using Akay.Be.Application.Features.LearningHubs.SignalRExample;
using Akay.Be.Application.Features.LearningHubs.TableStorageExamples;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Messaging;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

#pragma warning disable CS1591

namespace Akay.Be.Host.Controllers;


/// <summary>
/// Controller de demostración de funcionalidades de los paquetes nuget Akay.To
/// </summary>
/// <param name="dispatcher"></param>
/// <param name="streamDispatcher"></param>
/// <param name="messageBus"></param>
[SuppressMessage(
    "SonarAnalyzer.CSharp",
    "S6960:Controllers should not have too many responsibilities",
    Justification = "Controller de pruebas para demostrar dispatcher síncrono y streaming en un único lugar.")]
[ApiController]
[Route("api/learning-hubs")]
public sealed class LearningHubController(IDispatcher dispatcher,
                                          IStreamDispatcher streamDispatcher,
                                          IMessageBus messageBus) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting("writer-rate-limit")]
    [EndpointSummary("Lista Learning Hubs con filtros opcionales y paginación.")]
    [EndpointDescription("Demuestra PagedQuery y RateLimit.")]
    public async Task<IResult> GetAll([FromQuery] string? category,
                                      [FromQuery] string? status,
                                      [FromQuery] int? pageSize,
                                      [FromQuery] int? page,
                                      [FromQuery] bool? isAscending,
                                      [FromQuery] string? sortBy,
                                      CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubsQuery
        {
            Category = category,
            Status = status,
            PageSize = pageSize,
            Page = page,
            IsAscending = isAscending,
            SortBy = sortBy
        }, cancellationToken)).ToOk();

    [HttpGet("economics/translate")]
    [EndpointSummary("Traduce el texto de economia a ingles y frances.")]
    [EndpointDescription("Demuestra IQuery e ICognitiveTranslatorService.")]
    public async Task<IResult> TranslateEconomics(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new TranslateEconomicsTextQuery(), cancellationToken)).ToOk();

    [HttpGet("economics/speech")]
    [EndpointSummary("Genera audio TTS y lo transmite como audio/wav.")]
    [EndpointDescription("Demuestra IStreamQuery y la caché propia de ICognitiveSpeechService.")]
    public async Task SpeechEconomics(CancellationToken cancellationToken)
    {
        HttpContext.Response.ContentType = "audio/wav";
        await foreach (var chunk in streamDispatcher.Stream(new SpeechEconomicsTextRequest(), cancellationToken))
        {
            await HttpContext.Response.Body.WriteAsync(chunk, cancellationToken);
            await HttpContext.Response.Body.FlushAsync(cancellationToken);
        }
    }

    [HttpGet("{id:int}")]
    [EndpointSummary("Obtiene un Learning Hub por ID.")]
    [EndpointDescription("Demuestra IQuery e ICacheable.")]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubQuery(id), cancellationToken)).ToOk();

    [HttpGet("{id:int}/badge-uri")]
    [AllowAnonymous]
    [EndpointSummary("Obtiene o regenera la URI SAS del badge SVG.")]
    [EndpointDescription("Demuestra IQuery, IBlobCacheable y AllowAnonymous.")]
    public async Task<IResult> GetBadgeUri(int id, [FromQuery] bool forceRegenerate, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubBadgeUriQuery(id, forceRegenerate), cancellationToken)).ToOk();

    [HttpPost("search-stream")]
    [EndpointSummary("Busca Learning Hubs con respuesta incremental.")]
    [EndpointDescription("Demuestra IStreamQuery via IStreamDispatcher.")]
    public IAsyncEnumerable<LearningHubStreamItem> SearchStream([FromBody] SearchLearningHubsStreamRequest request, CancellationToken cancellationToken) =>
        streamDispatcher.Stream(request, cancellationToken);

    [HttpPost]
    [EnableRateLimiting("writer-rate-limit")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [EndpointSummary("Crea un Learning Hub con archivo adjunto.")]
    [EndpointDescription("Demuestra ICommand, ValidationBehavior, IRetryableRequest, ICompensableRequest y RateLimit.")]
    public async Task<IResult> Create([FromForm] string name,
                                        [FromForm] string description,
                                        [FromForm] string address,
                                       [FromForm] string category,
                                       [FromForm] int failedAttempts,
                                       IFormFile file,
                                       CancellationToken cancellationToken)
    {
        var command = new CreateLearningHubCommand(name,
                                                   description,
                                                   address,
                                                   category,
                                                   file.OpenReadStream(),
                                                   file.FileName,
                                                   file.ContentType,
                                                   failedAttempts);

        return (await dispatcher.Send(command, cancellationToken)).ToCreated(value => $"api/learning-hubs/{value.Id}");
    }

    [HttpPost("{id:int}/generate-report")]
    [EndpointSummary("Encola la generacion de un informe del Learning Hub.")]
    [EndpointDescription("Demuestra ICommandMessage y SendAsync sobre Rebus.")]
    public async Task<IResult> GenerateReport(int id, CancellationToken cancellationToken)
    {
        await messageBus.SendAsync(new GenerateLearningHubReportMessage(id), cancellationToken);
        return TypedResults.Accepted($"api/learning-hubs/{id}/generate-report");
    }

    [HttpPut("{id:int}")]
    [EndpointSummary("Actualiza un Learning Hub.")]
    [EndpointDescription("Demuestra ICommand y ValidationBehavior.")]
    public async Task<IResult> Update(int id, [FromBody] UpdateLearningHubRequest request, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new UpdateLearningHubCommand(id, request), cancellationToken)).ToNoContent();

    [HttpDelete("{id:int}")]
    [EndpointSummary("Elimina un Learning Hub.")]
    [EndpointDescription("Demuestra ICommand basico.")]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteLearningHubCommand(id), cancellationToken)).ToNoContent();

    [HttpGet("posts")]
    [EndpointSummary("Obtiene posts desde JSONPlaceholder.")]
    [EndpointDescription("Demuestra IQuery e IHttpClientFactory.")]
    public async Task<IResult> GetExternalPosts(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetPostsQuery(), cancellationToken)).ToOk();

    [HttpGet("posts/{postId:int}")]
    [EndpointSummary("Obtiene un post por ID desde JSONPlaceholder.")]
    [EndpointDescription("Demuestra IQuery e IHttpClientFactory.")]
    public async Task<IResult> GetExternalPostById(int postId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetPostByIdQuery(postId), cancellationToken)).ToOk();

    [HttpPost("posts")]
    [EndpointSummary("Crea un post en JSONPlaceholder.")]
    [EndpointDescription("Demuestra ICommand y POST JSON via IHttpClientFactory.")]
    public async Task<IResult> CreateExternalPost([FromBody] CreatePostCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command, cancellationToken)).ToCreated(value => $"api/learning-hubs/jsonplaceholder/posts/{value.Id}");

    [HttpPut("posts/{postId:int}")]
    [EndpointSummary("Actualiza un post en JSONPlaceholder.")]
    [EndpointDescription("Demuestra ICommand y PUT JSON via IHttpClientFactory.")]
    public async Task<IResult> UpdateExternalPost(int postId, [FromBody] UpdatePostCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { PostId = postId }, cancellationToken)).ToNoContent();

    // ─── Table Storage: MemoArray (objeto serializado) ─────────────────────

    [HttpPost("{id:int}/audit-logs")]
    [EndpointSummary("Guarda una entrada de auditoria en Table Storage.")]
    [EndpointDescription("Demuestra UpsertObjectAsync (MemoArray).")]
    public async Task<IResult> SaveAuditLog(
        int id,
        [FromBody] SaveLearningHubAuditLogRequest body,
        CancellationToken cancellationToken)
    {
        var command = new SaveLearningHubAuditLogCommand(id, body.Action, body.Details);
        return (await dispatcher.Send(command, cancellationToken)).ToCreated(_ => $"api/learning-hubs/{id}/audit-logs");
    }

    [HttpGet("{id:int}/audit-logs")]
    [EndpointSummary("Recupera auditorias de un Learning Hub desde Table Storage.")]
    [EndpointDescription("Demuestra GetObjectsByPartitionKeyAsync sobre MemoArray.")]
    public async Task<IResult> GetAuditLogs(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubAuditLogsQuery(id), cancellationToken)).ToOk();

    [HttpDelete("{id:int}/table-data")]
    [EndpointSummary("Elimina auditorias y metadatos de un Learning Hub en Table Storage.")]
    [EndpointDescription("Demuestra ExistsPartitionKeyAsync y DeleteEntitiesByPartitionKeyAsync.")]
    public async Task<IResult> DeleteTableData(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteLearningHubDataCommand(id), cancellationToken)).ToOk();

    // ─── Table Storage: Entidad por columnas ────────────────────────────────

    [HttpPost("{id:int}/metadata")]
    [EndpointSummary("Guarda metadatos de un Learning Hub como entidad por columnas.")]
    [EndpointDescription("Demuestra UpsertAsync.")]
    public async Task<IResult> SaveMetadata(
        int id,
        [FromBody] SaveLearningHubMetadataRequest body,
        CancellationToken cancellationToken)
    {
        var command = new SaveLearningHubMetadataCommand(id, body.TotalStudents.GetValueOrDefault(), body.TotalCourses.GetValueOrDefault(), body.AverageRating.GetValueOrDefault(), body.Tags);
        return (await dispatcher.Send(command, cancellationToken)).ToOk();
    }

    [HttpGet("{id:int}/metadata")]
    [EndpointSummary("Consulta metadatos paginados de Table Storage.")]
    [EndpointDescription("Demuestra QueryAsync con TableStorageFilter.")]
    public async Task<IResult> GetMetadata(
        int id,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetLearningHubMetadataQuery(id, pageSize, continuationToken);
        return (await dispatcher.Send(query, cancellationToken)).ToOk();
    }


    [HttpGet("{id:int}/send-signalr")]
    [EndpointSummary("Lanza una demo de envio SignalR para un Learning Hub.")]
    [EndpointDescription("Demuestra el envio de un comando que publica una notificacion en SignalR.")]
    public async Task<IResult> GetDemoSignalR(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DemoSignalRSendCommand(id), cancellationToken)).ToAccepted("");

}

/// <summary>
/// Request body para guardar una entrada de auditoria.
/// </summary>
public sealed record SaveLearningHubAuditLogRequest(string Action, string? Details);

/// <summary>
/// Request body para guardar metadatos de un Learning Hub.
/// </summary>
public sealed record SaveLearningHubMetadataRequest(int? TotalStudents, int? TotalCourses, double? AverageRating, string? Tags);

#pragma warning restore CS1591
