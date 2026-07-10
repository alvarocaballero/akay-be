using Akay.Be.Domain.Entities.Organization;
using Akay.To.Core.Application.Abstractions.Persistence;

namespace Akay.Be.Application.Abstractions.Persistence.Repositories.Organization;

public interface ICenterRepository : IBaseRepository<Center, int>
{
    Task<List<Center>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);
}
