using Akay.Be.Application.Features.LearningHubs.Responses;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;
using Akay.To.Core.Infrastructure.Extensions;

namespace Akay.Be.Application.Features.LearningHubs.HttpExamples;

public record CreatePostCommand(string Title, string Body, int UserId) : ICommand<PostResponse>;

/// <summary>
/// Handler que crea un post nuevo en JSONPlaceholder.
/// Demuestra PostJsonAsync con payload tipado.
/// El body se serializa automaticamente a JSON via PostAsJsonAsync.
/// </summary>
internal sealed class CreatePostHandler(IHttpClientFactory httpClientFactory) : ICommandHandler<CreatePostCommand, PostResponse>
{
    public async ValueTask<Result<PostResponse>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("JsonPlaceholder");

        var result = await client.PostJsonAsync<PostResponse, CreatePostCommand>(
            "posts", request, cancellationToken);

        if (result.IsFailure)
            return Result<PostResponse>.Failure(result.Error);

        return Result<PostResponse>.Success(result.Value!);
    }
}
