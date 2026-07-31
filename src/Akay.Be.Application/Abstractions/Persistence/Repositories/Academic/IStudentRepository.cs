using Akay.Be.Application.Features.Students;
using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Requests;
using Akay.To.Core.Application.Responses;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;

public interface IStudentRepository : IBaseRepository<Student, int>
{
    Task<List<Student>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    Task<List<Student>> GetByCenterIdAsync(int centerId, CancellationToken cancellationToken = default);
    Task<PagedResponse<List<StudentResponse>>> GetPagedByAdminScopeAsync(StudentListFilter filter, PageRequest pageRequest, CancellationToken cancellationToken = default);
    Task<StudentResponse?> GetByIdWithUserAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Student>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<Student>> GetByCenterIdsAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default);
    Task<bool> StudentExistsForUserAndCenterAsync(int userId, int centerId, CancellationToken cancellationToken = default);
    Task<Student?> GetByUserIdAndCenterIdAsync(int userId, int centerId, CancellationToken cancellationToken = default);
    Task<StudentDetailResponse?> GetStudentDetailsAsync(int studentId, int centerId, CancellationToken cancellationToken = default);
}
