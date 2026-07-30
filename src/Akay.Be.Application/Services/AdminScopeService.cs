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
        => await GetRolesCenterIdsAsync(UserRole.Admin, cancellationToken);

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

    public async Task<Result> EnsureCanAccessSubjectAsync(int subjectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var subject = await subjectRepository.GetWithCentersAsync(subjectId, cancellationToken);
        if (subject is null)
            return Error.NotFound(NotFoundCode, $"Asignatura {subjectId} no encontrada.");

        var adminCenters = await GetAdminCenterIdsAsync(cancellationToken);
        var subjectCenters = subject.Centers.Select(c => c.CenterId).ToHashSet();

        return subjectCenters.Overlaps(adminCenters)
            ? Result.Success()
            : Error.Forbidden(ForbiddenCode, $"No tienes permisos de administrador sobre la asignatura {subjectId}.");
    }

    public async Task<Result> EnsureCanAccessAcademicPeriodAsync(int academicPeriodId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var period = await academicPeriodRepository.GetByIdAsync(academicPeriodId, cancellationToken);
        if (period is null)
            return Error.NotFound(NotFoundCode, $"Periodo académico {academicPeriodId} no encontrado.");

        return await EnsureAdminOfCenterAsync(period.CenterId, cancellationToken);
    }

    public async Task<Result> EnsureCanAccessCourseAsync(int courseId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var centerId = await courseRepository.GetCenterIdAsync(courseId, cancellationToken);
        if (!centerId.HasValue)
            return Error.NotFound(NotFoundCode, $"Curso {courseId} no encontrado.");

        return await EnsureAdminOfCenterAsync(centerId.Value, cancellationToken);
    }

    public async Task<Result> EnsureCanAccessStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var student = await studentRepository.GetByIdAsync(studentId, cancellationToken);
        if (student is null)
            return Error.NotFound(NotFoundCode, $"Estudiante {studentId} no encontrado.");

        return await EnsureAdminOfCenterAsync(student.CenterId, cancellationToken);
    }

    public async Task<Result> EnsureCanAccessUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Error.NotFound(NotFoundCode, $"Usuario {userId} no encontrado.");

        var adminCenters = await GetAdminCenterIdsAsync(cancellationToken);
        var userCenterIds = user.RoleAssignments
            .Where(r => r.CenterId.HasValue)
            .Select(r => r.CenterId!.Value)
            .ToHashSet();

        return userCenterIds.Overlaps(adminCenters)
            ? Result.Success()
            : Error.Forbidden(ForbiddenCode, $"No tienes permisos de administrador sobre el usuario {userId}.");
    }

    public async Task<bool> UserHasRoleInCenterAsync(int userId, int centerId, UserRole role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await userRepository.UserHasActiveRoleInCenterAsync(userId, centerId, role, cancellationToken);
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
