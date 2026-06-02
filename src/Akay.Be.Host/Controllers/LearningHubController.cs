using System.Diagnostics.CodeAnalysis;
using Akay.Be.Application.Features.LearningHubs.BlobStorageExamples;
using Akay.Be.Application.Features.LearningHubs.CognitiveServicesExamples;
using Akay.Be.Application.Features.LearningHubs.HttpExamples;
using Akay.Be.Application.Features.LearningHubs.MediatorExamples;
using Akay.Be.Application.Features.LearningHubs.TableStorageExamples;
using Akay.To.Core.Host.Abstractions.Mediator;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Akay.Be.Host.Controllers;


/// <summary>
/// Controller de demostración de funcionalidades de los paquetes nuget Akay.To
/// </summary>
/// <param name="dispatcher"></param>
/// <param name="streamDispatcher"></param>
[SuppressMessage(
    "SonarAnalyzer.CSharp",
    "S6960:Controllers should not have too many responsibilities",
    Justification = "Controller de pruebas para demostrar dispatcher síncrono y streaming en un único lugar.")]
[ApiController]
[Route("api/learning-hubs")]
public sealed class LearningHubController(IDispatcher dispatcher, IStreamDispatcher streamDispatcher) : ControllerBase
{
    /// <summary>
    /// Lista Learning Hubs con filtros opcionales.
    /// Demuestra <c>IQuery</c> y <c>RateLimit</c>.
    /// </summary>
    [HttpGet]
    [EnableRateLimiting("writer-rate-limit")]
    public async Task<IResult> GetAll([FromQuery] string? category, [FromQuery] string? status, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubsQuery(category, status), cancellationToken)).ToOk();

    /// <summary>
    /// Traduce el texto de economia a ingles y frances.
    /// Demuestra <c>IQuery</c> y <c>ICognitiveTranslatorService</c>.
    /// </summary>
    [HttpGet("economics/translate")]
    public async Task<IResult> TranslateEconomics(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new TranslateEconomicsTextQuery(), cancellationToken)).ToOk();

    /// <summary>
    /// Genera audio TTS y lo transmite como <c>audio/wav</c>.
    /// Demuestra <c>IStreamQuery</c> y la caché propia de <c>ICognitiveSpeechService</c>.
    /// </summary>
    [HttpGet("economics/speech")]
    public async Task SpeechEconomics(CancellationToken cancellationToken)
    {
        HttpContext.Response.ContentType = "audio/wav";
        await foreach (var chunk in streamDispatcher.Stream(new SpeechEconomicsTextRequest(), cancellationToken))
        {
            await HttpContext.Response.Body.WriteAsync(chunk, cancellationToken);
            await HttpContext.Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Obtiene un Learning Hub por ID.
    /// Demuestra <c>IQuery</c> y <c>ICacheable</c>.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubQuery(id), cancellationToken)).ToOk();

    /// <summary>
    /// Obtiene o regenera la URI SAS del badge SVG.
    /// Demuestra <c>IQuery</c>, <c>IBlobCacheable</c> y <c>AllowAnonymous</c>.
    /// </summary>
    [HttpGet("{id:int}/badge-uri")]
    [AllowAnonymous]
    public async Task<IResult> GetBadgeUri(int id, [FromQuery] bool forceRegenerate, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubBadgeUriQuery(id, forceRegenerate), cancellationToken)).ToOk();

    /// <summary>
    /// Busca Learning Hubs con respuesta incremental.
    /// Demuestra <c>IStreamQuery</c> via <c>IStreamDispatcher</c>.
    /// </summary>
    [HttpPost("search-stream")]
    public IAsyncEnumerable<LearningHubStreamItem> SearchStream([FromBody] SearchLearningHubsStreamRequest request, CancellationToken cancellationToken) =>
        streamDispatcher.Stream(request, cancellationToken);

    /// <summary>
    /// Crea un Learning Hub con archivo adjunto.
    /// Demuestra <c>ICommand</c>, <c>ValidationBehavior</c>, <c>IRetryableRequest</c>, <c>ICompensableRequest</c> y <c>RateLimit</c>.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("writer-rate-limit")]
    [RequestSizeLimit(10 * 1024 * 1024)]
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

    /// <summary>
    /// Actualiza un Learning Hub.
    /// Demuestra <c>ICommand</c> y <c>ValidationBehavior</c>.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IResult> Update(int id, [FromBody] UpdateLearningHubRequest request, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new UpdateLearningHubCommand(id, request), cancellationToken)).ToNoContent();

    /// <summary>
    /// Elimina un Learning Hub.
    /// Demuestra <c>ICommand</c> basico.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteLearningHubCommand(id), cancellationToken)).ToNoContent();

    /// <summary>
    /// Obtiene posts desde JSONPlaceholder.
    /// Demuestra <c>IQuery</c> y <c>IHttpClientFactory</c>.
    /// </summary>
    [HttpGet("posts")]
    public async Task<IResult> GetExternalPosts(CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetPostsQuery(), cancellationToken)).ToOk();

    /// <summary>
    /// Obtiene un post por ID desde JSONPlaceholder.
    /// Demuestra <c>IQuery</c> y <c>IHttpClientFactory</c>.
    /// </summary>
    [HttpGet("posts/{postId:int}")]
    public async Task<IResult> GetExternalPostById(int postId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetPostByIdQuery(postId), cancellationToken)).ToOk();

    /// <summary>
    /// Crea un post en JSONPlaceholder.
    /// Demuestra <c>ICommand</c> y POST JSON via <c>IHttpClientFactory</c>.
    /// </summary>
    [HttpPost("posts")]
    public async Task<IResult> CreateExternalPost([FromBody] CreatePostCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command, cancellationToken)).ToCreated(value => $"api/learning-hubs/jsonplaceholder/posts/{value.Id}");

    /// <summary>
    /// Actualiza un post en JSONPlaceholder.
    /// Demuestra <c>ICommand</c> y PUT JSON via <c>IHttpClientFactory</c>.
    /// </summary>
    [HttpPut("posts/{postId:int}")]
    public async Task<IResult> UpdateExternalPost(int postId, [FromBody] UpdatePostCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command with { PostId = postId }, cancellationToken)).ToOk();

    // ─── Table Storage: MemoArray (objeto serializado) ─────────────────────

    /// <summary>
    /// Guarda una entrada de auditoria en Table Storage.
    /// Demuestra <c>UpsertObjectAsync</c> (MemoArray).
    /// </summary>
    [HttpPost("{id:int}/audit-logs")]
    public async Task<IResult> SaveAuditLog(
        int id,
        [FromBody] SaveLearningHubAuditLogRequest body,
        CancellationToken cancellationToken)
    {
        var command = new SaveLearningHubAuditLogCommand(id, body.Action, body.Details);
        return (await dispatcher.Send(command, cancellationToken)).ToCreated(_ => $"api/learning-hubs/{id}/audit-logs");
    }

    /// <summary>
    /// Recupera auditorias de un Learning Hub desde Table Storage.
    /// Demuestra <c>GetObjectsByPartitionKeyAsync</c> sobre MemoArray.
    /// </summary>
    [HttpGet("{id:int}/audit-logs")]
    public async Task<IResult> GetAuditLogs(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubAuditLogsQuery(id), cancellationToken)).ToOk();

    /// <summary>
    /// Elimina auditorias y metadatos de un Learning Hub en Table Storage.
    /// Demuestra <c>ExistsPartitionKeyAsync</c> y <c>DeleteEntitiesByPartitionKeyAsync</c>.
    /// </summary>
    [HttpDelete("{id:int}/table-data")]
    public async Task<IResult> DeleteTableData(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteLearningHubDataCommand(id), cancellationToken)).ToOk();

    // ─── Table Storage: Entidad por columnas ────────────────────────────────

    /// <summary>
    /// Guarda metadatos de un Learning Hub como entidad por columnas.
    /// Demuestra <c>UpsertAsync</c>.
    /// </summary>
    [HttpPost("{id:int}/metadata")]
    public async Task<IResult> SaveMetadata(
        int id,
        [FromBody] SaveLearningHubMetadataRequest body,
        CancellationToken cancellationToken)
    {
        var command = new SaveLearningHubMetadataCommand(id, body.TotalStudents.GetValueOrDefault(), body.TotalCourses.GetValueOrDefault(), body.AverageRating.GetValueOrDefault(), body.Tags);
        return (await dispatcher.Send(command, cancellationToken)).ToOk();
    }

    /// <summary>
    /// Consulta metadatos paginados de Table Storage.
    /// Demuestra <c>QueryAsync</c> con <c>TableStorageFilter</c>.
    /// </summary>
    [HttpGet("{id:int}/metadata")]
    public async Task<IResult> GetMetadata(
        int id,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? continuationToken = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetLearningHubMetadataQuery(id, pageSize, continuationToken);
        return (await dispatcher.Send(query, cancellationToken)).ToOk();
    }
}

/// <summary>
/// Request body para guardar una entrada de auditoria.
/// </summary>
public sealed record SaveLearningHubAuditLogRequest(string Action, string? Details);

/// <summary>
/// Request body para guardar metadatos de un Learning Hub.
/// </summary>
public sealed record SaveLearningHubMetadataRequest(
    int? TotalStudents,
    int? TotalCourses,
    double? AverageRating,
    string? Tags);
