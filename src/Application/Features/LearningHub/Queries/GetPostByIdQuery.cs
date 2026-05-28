using Akay.To.Core.Application.Abstractions.Mediator;

namespace Akay.Be.Application.Features.LearningHub.Queries;

/// <summary>
/// Query que obtiene un post por su id desde JSONPlaceholder.
/// Demuestra el uso de GetJsonAsync con URI absoluta.
/// Flujo: HttpClient -> GET /posts/{id} -> Deserializar a PostResponse -> Result.
/// </summary>
/// <param name="PostId">Id del post a buscar.</param>
public record GetPostByIdQuery(int PostId) : IQuery<Responses.PostResponse>;
