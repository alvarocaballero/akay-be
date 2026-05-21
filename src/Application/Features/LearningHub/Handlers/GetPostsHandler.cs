using Akay.Be.Application.Features.LearningHub.Queries;
using Akay.Be.Application.Features.LearningHub.Responses;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Infrastructure.Extensions;

namespace Akay.Be.Application.Features.LearningHub.Handlers;

/// <summary>
/// Handler que obtiene todos los posts de JSONPlaceholder.
/// Demuestra GetJsonAsync con endpoint relativo sobre el cliente "jsonplaceholder".
/// La BaseAddress ya esta configurada en appsettings.json apuntando a jsonplaceholder.org.
/// </summary>
internal sealed class GetPostsHandler(IHttpClientFactory httpClientFactory) : IQueryHandler<GetPostsQuery, List<PostResponse>>
{
    public async ValueTask<Result<List<PostResponse>>> Handle(GetPostsQuery request, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("jsonplaceholder");

        // GetJsonAsync soporta endpoints relativos: resuelve contra BaseAddress
        var result = await client.GetJsonAsync<List<PostResponse>>("v2/posts", cancellationToken);

        if (result.IsFailure)
            return Result<List<PostResponse>>.Failure(result.Error);

        return Result<List<PostResponse>>.Success(result.Value!);
    }
}
