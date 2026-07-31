using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Abstractions.Services;

/// <summary>
/// Resuelve el alcance administrativo del usuario autenticado.
/// </summary>
public interface IAdminScopeService
{
    /// <summary>
    /// Devuelve los IDs de los centros donde el usuario actual tiene rol Admin.
    /// </summary>
    Task<IReadOnlySet<int>> GetAdminCenterIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los IDs de los centros donde el usuario actual tiene rol Teacher.
    /// </summary>
    Task<IReadOnlySet<int>> GetTeacherCenterIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve los IDs de los centros donde el usuario actual tiene rol Admin o Teacher.
    /// </summary>
    Task<IReadOnlySet<int>> GetAdminOrTeacherCenterIdsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica que el usuario actual sea admin del centro indicado.
    /// </summary>
    Task<Result> EnsureAdminOfCenterAsync(int centerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica que el usuario actual sea teacher del centro indicado.
    /// </summary>
    Task<Result> EnsureTeacherOfCenterAsync(int centerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica que el usuario actual sea admin o teacher del centro indicado.
    /// </summary>
    Task<Result> EnsureAdminOrTeacherOfCenterAsync(int centerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica que el usuario actual sea admin de todos los centros indicados.
    /// </summary>
    Task<Result> EnsureAdminOfAllCentersAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default);

    Task<Result> EnsureCanAccessSubjectAsync(int subjectId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanWriteSubjectAsync(int subjectId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanReadSubjectContentAsync(int subjectId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanWriteSubjectContentAsync(int subjectId, CancellationToken cancellationToken = default);

    Task<Result> EnsureCanAccessAcademicPeriodAsync(int academicPeriodId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanWriteAcademicPeriodAsync(int academicPeriodId, CancellationToken cancellationToken = default);

    Task<Result> EnsureCanAccessCourseAsync(int courseId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanWriteCourseAsync(int courseId, CancellationToken cancellationToken = default);

    Task<Result> EnsureCanAccessStudentAsync(int studentId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanWriteStudentAsync(int studentId, CancellationToken cancellationToken = default);

    Task<Result> EnsureCanAccessUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<Result> EnsureCanWriteUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica que el usuario objetivo tenga el rol especificado en el centro indicado.
    /// </summary>
    Task<bool> UserHasRoleInCenterAsync(int userId, int centerId, Domain.Enums.UserRole role, CancellationToken cancellationToken = default);
}
