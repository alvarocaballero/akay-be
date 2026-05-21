using Akay.Be.Application.Features.LearningHub.Commands;
using Akay.Be.Application.Features.LearningHub.Responses;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Infrastructure.Extensions;

namespace Akay.Be.Application.Features.LearningHub.Handlers;

/// <summary>
/// Handler que actualiza un post existente en JSONPlaceholder.
/// Demuestra PutJsonAsync con payload tipado y endpoint relativo con path parameter.
/// </summary>
internal sealed class UpdatePostHandler(IHttpClientFactory httpClientFactory) : ICommandHandler<UpdatePostCommand, PostResponse>
{
    public async ValueTask<Result<PostResponse>> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("jsonplaceholder");

        var result = await client.PutJsonAsync<PostResponse, UpdatePostCommand>(
            $"v2/posts/{request.PostId}", request, cancellationToken);

        if (result.IsFailure)
            return Result<PostResponse>.Failure(result.Error);

        return Result<PostResponse>.Success(result.Value!);
    }
}
