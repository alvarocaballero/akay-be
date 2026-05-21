namespace Akay.Be.Application.Features.LearningHub.Responses;

/// <summary>
/// Respuesta para un post de JSONPlaceholder.
/// Contiene el id del post, el titulo, el contenido y el id del autor.
/// Se usa como tipo de retorno para queries y commands que trabajan con posts.
/// </summary>
public record PostResponse(int Id, string Title, string Body, int UserId);
