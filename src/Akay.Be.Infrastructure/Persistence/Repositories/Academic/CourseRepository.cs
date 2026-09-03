using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Features.CourseStudents;
using Akay.Be.Application.Features.CourseSubjectStudents;
using Akay.Be.Application.Features.CourseSubjectTeachers;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.EF.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Academic;

internal sealed class CourseRepository(ApplicationDbContext context) : BaseRepository<Course, int>(context), ICourseRepository
{

    public async Task<Course?> GetWithSubjectsAsync(int id, bool readOnly = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Course> query = readOnly ? Set.AsNoTracking() : Set;
        return await query
            .Include(x => x.Subjects)
                .ThenInclude(s => s.Teachers)
            .Include(x => x.Subjects)
                .ThenInclude(s => s.Students)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Course?> GetWithStudentsAsync(int id, bool readOnly = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Course> query = readOnly ? Set.AsNoTracking() : Set;
        return await query
            .Include(x => x.Students)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<Course>> GetByAcademicPeriodIdAsync(int academicPeriodId, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(x => x.AcademicPeriodId == academicPeriodId)
            .ToListAsync(cancellationToken);

    public async Task<bool> CodeExistsInPeriodAsync(int academicPeriodId, string code, int? excludingId = null, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .AnyAsync(x => x.AcademicPeriodId == academicPeriodId
                           && x.Code == code
                           && (!excludingId.HasValue || x.Id != excludingId.Value),
                      cancellationToken);

    public async Task<bool> CourseBelongsToCenterAsync(int courseId, int centerId, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(x => x.Id == courseId && x.AcademicPeriod.CenterId == centerId)
            .AnyAsync(cancellationToken);

    public async Task<int?> GetCenterIdAsync(int courseId, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(x => x.Id == courseId)
            .Select(x => (int?)x.AcademicPeriod.CenterId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<List<Course>> GetByCenterIdsAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default)
    {
        var ids = centerIds.ToHashSet();
        return await Set
            .AsNoTracking()
            .Include(x => x.AcademicPeriod)
            .Where(x => ids.Contains(x.AcademicPeriod.CenterId))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CourseStudentResponse>> GetStudentsWithUsersByCourseAsync(int courseId, CancellationToken cancellationToken = default)
        => await context.Set<StudentCourse>()
            .AsNoTracking()
            .Where(sc => sc.CourseId == courseId)
            .Select(sc => new CourseStudentResponse(sc.CourseId,
                                                    sc.UserId,
                                                    sc.Id,
                                                    sc.User.FirstName,
                                                    sc.User.LastName,
                                                    sc.User.Email))
            .ToListAsync(cancellationToken);

    public async Task<Course?> GetWithFullGraphAsync(int id, bool readOnly = false, CancellationToken cancellationToken = default)
    {
        IQueryable<Course> query = readOnly ? Set.AsNoTracking() : Set;
        return await query
            .AsSplitQuery()
            .Include(x => x.AcademicPeriod)
            .Include(x => x.Subjects)
                .ThenInclude(x => x.Subject)
            .Include(x => x.Subjects)
                .ThenInclude(s => s.Teachers)
            .Include(x => x.Subjects)
                .ThenInclude(s => s.Students)
            .Include(x => x.Students)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<List<CourseSubjectStudentResponse>> GetCourseSubjectStudentsWithDetailsAsync(int courseId,
                                                                                                   int subjectId,
                                                                                                   CancellationToken cancellationToken = default)
        => await context.Set<CourseSubjectStudent>()
            .AsNoTracking()
            .Where(css => css.CourseSubject.CourseId == courseId && css.CourseSubject.SubjectId == subjectId)
            .Select(css => new CourseSubjectStudentResponse(css.CourseSubject.CourseId,
                                                            css.CourseSubject.SubjectId,
                                                            css.StudentCourse.UserId,
                                                            css.StudentCourse.Id,
                                                            context.Students
                                                                .Where(student => student.UserId == css.StudentCourse.UserId
                                                                               && student.CenterId == css.CourseSubject.Course.AcademicPeriod.CenterId)
                                                                .Select(student => student.StudentNumber)
                                                                .FirstOrDefault(),
                                                            css.StudentCourse.User.FirstName,
                                                            css.StudentCourse.User.LastName,
                                                            css.StudentCourse.User.Email))
            .ToListAsync(cancellationToken);

    public async Task SoftDeleteStudentEnrollmentsAsync(int userId,
                                                         int? centerId = null,
                                                         CancellationToken cancellationToken = default)
    {
        var deletedAt = DateTimeOffset.UtcNow;
        var subjectEnrollments = context.CourseSubjectStudents
            .IgnoreQueryFilters()
            .Where(enrollment => enrollment.StudentCourse.UserId == userId
                              && (!centerId.HasValue
                                  || enrollment.CourseSubject.Course.AcademicPeriod.CenterId == centerId.Value));

        await subjectEnrollments.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.DeletedAt, deletedAt), cancellationToken);

        var courseEnrollments = context.StudentCourses
            .IgnoreQueryFilters()
            .Where(enrollment => enrollment.UserId == userId
                              && (!centerId.HasValue
                                  || enrollment.Course.AcademicPeriod.CenterId == centerId.Value));

        await courseEnrollments.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.DeletedAt, deletedAt), cancellationToken);
    }

    public async Task<List<CourseSubjectTeacherResponse>> GetCourseSubjectTeachersWithDetailsAsync(int courseId,
                                                                                                   int subjectId,
                                                                                                   CancellationToken cancellationToken = default)
        => await context.Set<CourseSubjectTeacher>()
            .AsNoTracking()
            .Where(cst => cst.CourseSubject.CourseId == courseId && cst.CourseSubject.SubjectId == subjectId)
            .Select(cst => new CourseSubjectTeacherResponse(cst.CourseSubject.CourseId,
                                                            cst.CourseSubject.SubjectId,
                                                            cst.UserId,
                                                            cst.User.FirstName,
                                                            cst.User.LastName,
                                                            cst.User.Email))
            .ToListAsync(cancellationToken);
}
