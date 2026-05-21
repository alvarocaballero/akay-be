using Akay.To.Core.Application.Abstractions.Mediator;

namespace Akay.Be.Application.Features.LearningHub.Commands;

/// <summary>
/// Command que actualiza un post existente en JSONPlaceholder.
/// Demuestra el uso de PutJsonAsync.
/// Flujo: HttpClient -> PUT /posts/{id} -> Deserializar a PostResponse -> Result.
/// </summary>
/// <param name="PostId">Id del post a actualizar.</param>
/// <param name="Title">Nuevo titulo.</param>
/// <param name="Body">Nuevo contenido.</param>
/// <param name="UserId">Id del autor.</param>
public record UpdatePostCommand(int PostId, string Title, string Body, int UserId) : ICommand<Responses.PostResponse>;
