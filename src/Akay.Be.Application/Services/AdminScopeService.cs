using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Services;

internal sealed class AdminScopeService(IUserContext userContext,
                                        IUserRepository userRepository,
                                        ISubjectRepository subjectRepository,
                                        IAcademicPeriodRepository academicPeriodRepository,
                                        ICourseRepository courseRepository,
                                        IStudentRepository studentRepository) : IAdminScopeService
{
    private const string ForbiddenCode = "admin.forbidden";
    private const string NotFoundCode = "admin.not_found";

    public async Task<IReadOnlySet<int>> GetAdminCenterIdsAsync(CancellationToken cancellationToken = default)
        => await GetRolesCenterIdsAsync(UserRole.Admin, cancellationToken);

    public async Task<IReadOnlySet<int>> GetTeacherCenterIdsAsync(CancellationToken cancellationToken = default)
        => await GetRolesCenterIdsAsync(UserRole.Teacher, cancellationToken);

    public async Task<IReadOnlySet<int>> GetAdminOrTeacherCenterIdsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var currentUserId = userContext.UserId;
        if (currentUserId <= 0)
            return new HashSet<int>();

        var rolesByCenter = await userRepository.GetUserRolesByCentersAsync(currentUserId, cancellationToken);
        return rolesByCenter
            .Where(kv => kv.Value.Contains(UserRole.Admin) || kv.Value.Contains(UserRole.Teacher))
            .Select(kv => kv.Key)
            .ToHashSet();
    }

    public async Task<Result> EnsureAdminOfCenterAsync(int centerId, CancellationToken cancellationToken = default)
    {
        var centers = await GetAdminCenterIdsAsync(cancellationToken);
        return centers.Contains(centerId)
            ? Result.Success()
            : Error.Forbidden(ForbiddenCode, $"No tienes permisos de administrador sobre el centro {centerId}.");
    }

    public async Task<Result> EnsureTeacherOfCenterAsync(int centerId, CancellationToken cancellationToken = default)
    {
        var centers = await GetTeacherCenterIdsAsync(cancellationToken);
        return centers.Contains(centerId)
            ? Result.Success()
            : Error.Forbidden(ForbiddenCode, $"No tienes permisos de profesor sobre el centro {centerId}.");
    }

    public async Task<Result> EnsureAdminOrTeacherOfCenterAsync(int centerId, CancellationToken cancellationToken = default)
    {
        var centers = await GetAdminOrTeacherCenterIdsAsync(cancellationToken);
        return centers.Contains(centerId)
            ? Result.Success()
            : Error.Forbidden(ForbiddenCode, $"No tienes permisos de admin, ni de profesor sobre el centro {centerId}.");
    }

    public async Task<Result> EnsureAdminOfAllCentersAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default)
    {
        var requestedCenters = centerIds.ToHashSet();
        if (requestedCenters.Count == 0)
            return Error.Validation("admin.centers_required", "Debe indicar al menos un centro.");

        var adminCenters = await GetAdminCenterIdsAsync(cancellationToken);
        var missing = requestedCenters.Where(id => !adminCenters.Contains(id)).ToList();

        return missing.Count == 0
            ? Result.Success()
            : Error.Forbidden(ForbiddenCode, $"No tienes permisos de administrador sobre los centros: {string.Join(", ", missing)}.");
    }

    public Task<Result> EnsureCanAccessSubjectAsync(int subjectId, CancellationToken cancellationToken = default)
        => EnsureResourceAccessAsync(subjectId,
                                     async (id, ct) => (await subjectRepository.GetWithCentersAsync(id, ct))?.Centers
                                         .Select(x => x.CenterId)
                                         .ToHashSet(),
                                     false,
                                     "Asignatura",
                                     cancellationToken);

    public Task<Result> EnsureCanWriteSubjectAsync(int subjectId, CancellationToken cancellationToken = default)
        => EnsureResourceAccessAsync(subjectId,
                                     async (id, ct) => (await subjectRepository.GetWithCentersAsync(id, ct))?.Centers
                                         .Select(x => x.CenterId)
                                         .ToHashSet(),
                                     true,
                                     "Asignatura",
                                     cancellationToken);

    public Task<Result> EnsureCanReadSubjectContentAsync(int subjectId, CancellationToken cancellationToken = default)
        => EnsureCanAccessSubjectAsync(subjectId, cancellationToken);

    public async Task<Result> EnsureCanWriteSubjectContentAsync(int subjectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var subject = await subjectRepository.GetWithAdminsAsync(subjectId, cancellationToken);
        if (subject is null)
            return Error.NotFound(NotFoundCode, $"Asignatura {subjectId} no encontrada.");

        return subject.Admins.Any(x => x.UserId == userContext.UserId)
            ? Result.Success()
            : Error.Forbidden(ForbiddenCode, $"No tienes permisos para modificar el contenido de la asignatura {subjectId}.");
    }

    public Task<Result> EnsureCanAccessAcademicPeriodAsync(int academicPeriodId, CancellationToken cancellationToken = default)
        => EnsureResourceAccessAsync(academicPeriodId, async (id, ct) =>
        {
            var period = await academicPeriodRepository.GetByIdAsync(id, ct);
            return period is null ? null : [period.CenterId];
        }, false, "Periodo académico", cancellationToken);

    public Task<Result> EnsureCanWriteAcademicPeriodAsync(int academicPeriodId, CancellationToken cancellationToken = default)
        => EnsureResourceAccessAsync(academicPeriodId, async (id, ct) =>
        {
            var period = await academicPeriodRepository.GetByIdAsync(id, ct);
            return period is null ? null : [period.CenterId];
        }, true, "Periodo académico", cancellationToken);

    public Task<Result> EnsureCanAccessCourseAsync(int courseId, CancellationToken cancellationToken = default)
        => EnsureResourceAccessAsync(courseId, async (id, ct) =>
        {
            var centerId = await courseRepository.GetCenterIdAsync(id, ct);
            return centerId is null ? null : [centerId.Value];
        }, false, "Curso", cancellationToken);

    public Task<Result> EnsureCanWriteCourseAsync(int courseId, CancellationToken cancellationToken = default)
        => EnsureResourceAccessAsync(courseId, async (id, ct) =>
        {
            var centerId = await courseRepository.GetCenterIdAsync(id, ct);
            return centerId is null ? null : [centerId.Value];
        }, true, "Curso", cancellationToken);

    public async Task<Result> EnsureCanAccessStudentAsync(int userId,
                                                           int centerId,
                                                           CancellationToken cancellationToken = default)
    {
        var student = await studentRepository.GetByUserIdAndCenterIdAsync(userId, centerId, cancellationToken);
        if (student is null)
            return Error.NotFound(NotFoundCode, $"Estudiante {userId} no encontrado en el centro {centerId}.");

        return await EnsureAdminOrTeacherOfCenterAsync(centerId, cancellationToken);
    }

    public async Task<Result> EnsureCanWriteStudentAsync(int userId,
                                                          int centerId,
                                                          CancellationToken cancellationToken = default)
    {
        var student = await studentRepository.GetByUserIdAndCenterIdAsync(userId, centerId, cancellationToken);
        if (student is null)
            return Error.NotFound(NotFoundCode, $"Estudiante {userId} no encontrado en el centro {centerId}.");

        return await EnsureAdminOfCenterAsync(centerId, cancellationToken);
    }

    public Task<Result> EnsureCanAccessUserAsync(int userId, CancellationToken cancellationToken = default)
        => EnsureResourceAccessAsync(userId, async (id, ct) =>
        {
            var user = await userRepository.GetByIdAsync(id, ct);
            return user?.RoleAssignments
                .Where(x => x.CenterId.HasValue)
                .Select(x => x.CenterId!.Value)
                .ToHashSet();
        }, true, "Usuario", cancellationToken);

    public Task<Result> EnsureCanWriteUserAsync(int userId, CancellationToken cancellationToken = default)
        => EnsureResourceAccessAsync(userId, async (id, ct) =>
        {
            var user = await userRepository.GetByIdAsync(id, ct);
            return user?.RoleAssignments
                .Where(x => x.CenterId.HasValue)
                .Select(x => x.CenterId!.Value)
                .ToHashSet();
        }, true, "Usuario", cancellationToken);

    public async Task<bool> UserHasRoleInCenterAsync(int userId, int centerId, UserRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await userRepository.UserHasActiveRoleInCenterAsync(userId, centerId, role, cancellationToken);
    }

    private async Task<Result> EnsureResourceAccessAsync(int resourceId,
                                                         Func<int, CancellationToken, Task<HashSet<int>?>> getResourceCenterIds,
                                                         bool isWrite,
                                                         string resourceName,
                                                         CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resourceCenterIds = await getResourceCenterIds(resourceId, cancellationToken);
        if (resourceCenterIds is null)
            return Error.NotFound(NotFoundCode, $"{resourceName} {resourceId} no encontrado.");

        var userCenters = isWrite
            ? await GetAdminCenterIdsAsync(cancellationToken)
            : await GetAdminOrTeacherCenterIdsAsync(cancellationToken);

        return resourceCenterIds.Overlaps(userCenters)
            ? Result.Success()
            : Error.Forbidden(ForbiddenCode, $"No tienes permisos sobre {resourceName} {resourceId}.");
    }

    private async Task<IReadOnlySet<int>> GetRolesCenterIdsAsync(UserRole? role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var currentUserId = userContext.UserId;
        if (currentUserId <= 0)
            return new HashSet<int>();

        var rolesByCenter = await userRepository.GetUserRolesByCentersAsync(currentUserId, cancellationToken);
        return rolesByCenter
            .Where(kv => role is null || kv.Value.Contains(role.Value))
            .Select(kv => kv.Key)
            .ToHashSet();
    }
}
