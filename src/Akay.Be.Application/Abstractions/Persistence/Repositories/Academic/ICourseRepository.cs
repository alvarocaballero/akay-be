using Akay.Be.Application.Features.CourseStudents;
using Akay.Be.Application.Features.CourseSubjectStudents;
using Akay.Be.Application.Features.CourseSubjectTeachers;
using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Persistence;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;

public interface ICourseRepository : IBaseRepository<Course, int>
{
    Task<Course?> GetWithSubjectsAsync(int id, CancellationToken cancellationToken = default);
    Task<Course?> GetWithStudentsAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Course>> GetByAcademicPeriodIdAsync(int academicPeriodId, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsInPeriodAsync(int academicPeriodId, string code, int? excludingId = null, CancellationToken cancellationToken = default);
    Task<bool> CourseBelongsToCenterAsync(int courseId, int centerId, CancellationToken cancellationToken = default);
    Task<int?> GetCenterIdAsync(int courseId, CancellationToken cancellationToken = default);
    Task<List<Course>> GetByCenterIdsAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default);
    Task<Course?> GetWithFullGraphAsync(int id, CancellationToken cancellationToken = default);
    Task<List<CourseStudentResponse>> GetStudentsWithUsersByCourseAsync(int courseId, CancellationToken cancellationToken = default);
    Task<List<CourseSubjectStudentResponse>> GetCourseSubjectStudentsWithDetailsAsync(int courseId, int subjectId, CancellationToken cancellationToken = default);
    Task<List<CourseSubjectTeacherResponse>> GetCourseSubjectTeachersWithDetailsAsync(int courseId, int subjectId, CancellationToken cancellationToken = default);
}
