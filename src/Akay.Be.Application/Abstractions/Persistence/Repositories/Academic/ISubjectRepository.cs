using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Persistence;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;

public interface ISubjectRepository : IBaseRepository<Subject, int>
{
    Task<Subject?> GetWithCentersAsync(int id, CancellationToken cancellationToken = default);
    Task<Subject?> GetWithAdminsAsync(int id, CancellationToken cancellationToken = default);
    Task<List<Subject>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Subject>> GetByCenterIdsAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default);
    Task<bool> SubjectIsAvailableForCenterAsync(int subjectId, int centerId, CancellationToken cancellationToken = default);
    Task<bool> SubjectBelongsToAnyCenterAsync(int subjectId, IEnumerable<int> centerIds, CancellationToken cancellationToken = default);
}
