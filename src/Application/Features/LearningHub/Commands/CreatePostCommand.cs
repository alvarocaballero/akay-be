using Akay.To.Core.Application.Abstractions.Mediator;

namespace Akay.Be.Application.Features.LearningHub.Commands;

/// <summary>
/// Command que crea un post nuevo en JSONPlaceholder.
/// Demuestra el uso de PostJsonAsync.
/// Flujo: HttpClient -> POST /posts -> Deserializar a PostResponse -> Result.
/// </summary>
/// <param name="Title">Titulo del post.</param>
/// <param name="Body">Contenido del post.</param>
/// <param name="UserId">Id del autor.</param>
public record CreatePostCommand(string Title, string Body, int UserId) : ICommand<Responses.PostResponse>;
