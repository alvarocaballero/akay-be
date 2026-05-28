using Akay.Be.Application.Features.LearningHub.Queries;
using Akay.Be.Application.Features.LearningHub.Responses;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Infrastructure.Extensions;

namespace Akay.Be.Application.Features.LearningHub.Handlers;

/// <summary>
/// Handler que obtiene un post por su id desde JSONPlaceholder.
/// Demuestra GetJsonAsync con URI absoluta.
/// </summary>
internal sealed class GetPostByIdHandler(IHttpClientFactory httpClientFactory) : IQueryHandler<GetPostByIdQuery, PostResponse>
{
    public async ValueTask<Result<PostResponse>> Handle(GetPostByIdQuery request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("jsonplaceholder");

        // GetJsonAsync con Uri absoluta: no depende de BaseAddress
        var result = await client.GetJsonAsync<PostResponse>(
            new Uri($"https://jsonplaceholder.org/v2/posts/{request.PostId}"), cancellationToken);

        if (result.IsFailure)
            return Result<PostResponse>.Failure(result.Error);

        return Result<PostResponse>.Success(result.Value!);
    }
}
