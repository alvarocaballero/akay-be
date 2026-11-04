using Akay.Be.Application.Features.TopicContents;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591

namespace Akay.Be.Host.Controllers;

/// <summary>
/// Gestión de contenidos de un tema.
/// </summary>
[ApiController]
[Route("api")]
[Tags("Topic contents")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class TopicContentsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost("topics/{topicId:int}/contents")]
    [Consumes("multipart/form-data")]
    public async Task<IResult> Upload(int topicId, IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return (await dispatcher.Send(new UploadTopicContentCommand(topicId, Path.GetFileName(file.FileName), file.ContentType, file.Length, stream), cancellationToken)).ToOk();
    }

    [HttpGet("topics/{topicId:int}/contents")]
    [EndpointSummary("Lista los contenidos de un tema.")]
    [ProducesResponseType<IReadOnlyList<TopicContentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetByTopic(int topicId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetTopicContentsQuery(topicId), cancellationToken)).ToOk();

    [HttpDelete("topics/{topicId:int}/contents/{id:int}")]
    [EndpointSummary("Elimina un contenido de un tema (soft-delete).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int topicId, int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteTopicContentCommand(topicId, id), cancellationToken)).ToNoContent();
}

#pragma warning restore CS1591
