using System.Diagnostics.CodeAnalysis;
using Akay.Be.Application.Features.LearningHubs;
using Akay.To.Core.Host.Abstractions.Mediator;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;


namespace Akay.Be.Host.Controllers;

[SuppressMessage(
    "SonarAnalyzer.CSharp",
    "S6960:Controllers should not have too many responsibilities",
    Justification = "Controller de pruebas para demostrar dispatcher s�ncrono y streaming en un �nico lugar.")]
[ApiController]
[Route("api/learning-hubs")]
public sealed class LearningHubController(IDispatcher dispatcher, IStreamDispatcher streamDispatcher) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting("writer-rate-limit")]
    public async Task<IResult> GetAll([FromQuery] string? category, [FromQuery] string? status, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubsQuery(category, status), cancellationToken)).ToOk();

    [HttpGet("{id:int}")]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubQuery(id), cancellationToken)).ToOk();

    [HttpGet("{id:int}/badge-uri")]
    [AllowAnonymous]
    public async Task<IResult> GetBadgeUri(int id, [FromQuery] bool forceRegenerate, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubBadgeUriQuery(id, forceRegenerate), cancellationToken)).ToOk();

    [HttpPost("search-stream")]
    public IAsyncEnumerable<LearningHubStreamItem> SearchStream([FromBody] SearchLearningHubsStreamRequest request, CancellationToken cancellationToken) =>
        streamDispatcher.Stream(request, cancellationToken);

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


    [HttpPut("{id:int}")]
    public async Task<IResult> Update(int id, [FromBody] UpdateLearningHubRequest request, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new UpdateLearningHubCommand(id, request), cancellationToken)).ToNoContent();


    [HttpDelete("{id:int}")]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteLearningHubCommand(id), cancellationToken)).ToNoContent();
    
}
