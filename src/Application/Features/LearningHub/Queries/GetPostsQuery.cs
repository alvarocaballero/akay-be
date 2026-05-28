using Akay.To.Core.Application.Abstractions.Mediator;

namespace Akay.Be.Application.Features.LearningHub.Queries;

/// <summary>
/// Query que obtiene todos los posts de JSONPlaceholder.
/// Demuestra el uso de GetJsonAsync con endpoint relativo sobre el cliente configurado.
/// Flujo: HttpClient -> GET /posts -> Deserializar a PostResponse[] -> Result.
/// </summary>
public record GetPostsQuery : IQuery<List<Responses.PostResponse>>;
