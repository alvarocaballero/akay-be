namespace Akay.Be.Application.Features.Users;

/// <summary>Local errors raised by user-related features.</summary>
public static class UserErrors
{
    public static Akay.To.Core.Application.Results.Error EmailExists() =>
        Akay.To.Core.Application.Results.Error.Conflict("user.email_exists", "Ya existe un usuario con ese email.");

    public static Akay.To.Core.Application.Results.Error ExternalEmailExists() =>
        Akay.To.Core.Application.Results.Error.Conflict("user.external_email_exists", "Ya existe un usuario con ese email en el proveedor de identidad.");

    public static Akay.To.Core.Application.Results.Error SuperAdminNotAllowed() =>
        Akay.To.Core.Application.Results.Error.Forbidden("user.superadmin_not_allowed", "No se puede asignar el rol SuperAdmin al crear un usuario.");

    public static Akay.To.Core.Application.Results.Error StudentNotAllowed() =>
        Akay.To.Core.Application.Results.Error.Forbidden("user.student_not_allowed", "No se puede crear un usuario con rol Student desde este endpoint.");

    public static Akay.To.Core.Application.Results.Error NotFound(int id) =>
        Akay.To.Core.Application.Results.Error.NotFound("user.not_found", $"Usuario {id} no encontrado.");
}
