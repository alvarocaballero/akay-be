using Akay.Be.Application.Features.LearningHub.Commands;
using Akay.Be.Application.Features.LearningHub.Responses;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Infrastructure.Extensions;

namespace Akay.Be.Application.Features.LearningHub.Handlers;

/// <summary>
/// Handler que crea un post nuevo en JSONPlaceholder.
/// Demuestra PostJsonAsync con payload tipado.
/// El body se serializa automaticamente a JSON via PostAsJsonAsync.
/// </summary>
internal sealed class CreatePostHandler(IHttpClientFactory httpClientFactory) : ICommandHandler<CreatePostCommand, PostResponse>
{
    public async ValueTask<Result<PostResponse>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("jsonplaceholder");

        var result = await client.PostJsonAsync<PostResponse, CreatePostCommand>(
            "v2/posts", request, cancellationToken);

        if (result.IsFailure)
            return Result<PostResponse>.Failure(result.Error);

        return Result<PostResponse>.Success(result.Value!);
    }
}
