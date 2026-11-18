using Akay.Be.Application.Features.TopicContents;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Host.DependencyInjection;
using Akay.To.Core.Host.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

#pragma warning disable CS1591
namespace Akay.Be.Host.Controllers;

[ApiController]
[Route("api/webhooks")]
[Authorize(AuthenticationSchemes = AuthenticationSchemeNames.ApiKey)]
public sealed class TopicConversionWebhookController(IDispatcher dispatcher) : ControllerBase
{
    [HttpPost("topic-content-converted")]
    public async Task<IResult> Converted([FromBody] SubjectTopicConvertedCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command, cancellationToken)).ToNoContent();
}
#pragma warning restore CS1591
