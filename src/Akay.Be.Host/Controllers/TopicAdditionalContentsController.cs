using Akay.Be.Application.Features.TopicAdditionalContents;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591

namespace Akay.Be.Host.Controllers;

/// <summary>
/// Gestión de contenidos adicionales de un tema.
/// </summary>
[ApiController]
[Route("api")]
[Tags("Topic additional contents")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class TopicAdditionalContentsController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet("topics/{topicId:int}/additional-contents")]
    [EndpointSummary("Lista los contenidos adicionales de un tema.")]
    [ProducesResponseType<IReadOnlyList<TopicAdditionalContentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetByTopic(int topicId, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetTopicAdditionalContentsQuery(topicId), cancellationToken)).ToOk();

    [HttpDelete("topics/{topicId:int}/additional-contents/{id:int}")]
    [EndpointSummary("Elimina un contenido adicional de un tema (soft-delete).")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> Delete(int topicId, int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteTopicAdditionalContentCommand(topicId, id), cancellationToken)).ToNoContent();
}

#pragma warning restore CS1591
