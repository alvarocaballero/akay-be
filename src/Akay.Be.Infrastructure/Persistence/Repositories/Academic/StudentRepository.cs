using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Features.Students;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.Core.Application.Requests;
using Akay.To.Core.Application.Responses;
using Akay.To.EF.Infrastructure.Queries;
using Akay.To.EF.Infrastructure.Repositories;
using Akay.To.EF.Infrastructure.Specifications;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Persistence.Repositories.Academic;

internal sealed class StudentRepository(ApplicationDbContext context) : BaseRepository<Student, int>(context), IStudentRepository
{
    public async Task<List<Student>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

    public async Task<StudentResponse?> GetByIdWithUserAsync(int id, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new StudentResponse(x.Id,
                                             x.UserId,
                                             x.CenterId,
                                             x.StudentNumber,
                                             x.IsActive,
                                             x.User.FirstName,
                                             x.User.LastName,
                                             x.User.Email))
            .FirstOrDefaultAsync(cancellationToken);

#pragma warning disable S3776
    public async Task<PagedResponse<List<StudentResponse>>> GetPagedByAdminScopeAsync(
#pragma warning restore S3776
        StudentListFilter filter, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        var query = Set.ApplySpecification(StudentSpecifications.ByCenterIds(filter.CenterIds));

        if (filter.IsActive.HasValue)
        {
            query = query.ApplySpecification(StudentSpecifications.IsActive(filter.IsActive.Value));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.ApplySpecification(StudentSpecifications.NumberSearch(SqlHelpers.Contains(filter.Search.Trim())));
        }

        var sortMap = new Dictionary<string, Func<IQueryable<Student>, bool, IOrderedQueryable<Student>>>
        {
            ["studentNumber"] = (q, asc) => asc ? q.OrderBy(x => x.StudentNumber) : q.OrderByDescending(x => x.StudentNumber),
            ["isActive"] = (q, asc) => asc ? q.OrderBy(x => x.IsActive) : q.OrderByDescending(x => x.IsActive),
            ["userId"] = (q, asc) => asc ? q.OrderBy(x => x.UserId) : q.OrderByDescending(x => x.UserId),
            ["centerId"] = (q, asc) => asc ? q.OrderBy(x => x.CenterId) : q.OrderByDescending(x => x.CenterId)
        };

        var orderedQuery = query.ApplyOrdering(pageRequest.SortBy,
                                               pageRequest.IsAscending,
                                               sortMap,
                                               (q, asc) => asc ? q.OrderBy(x => x.Id) : q.OrderByDescending(x => x.Id));

        int normalizedPage = pageRequest.Page is > 0 ? pageRequest.Page.Value : 1;
        int normalizedPageSize = pageRequest.PageSize is > 0 ? pageRequest.PageSize.Value : 100;

        List<StudentResponse> items = await orderedQuery
            .AsNoTracking()
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize + 1)
            .Select(student => new StudentResponse(student.Id,
                                                   student.UserId,
                                                   student.CenterId,
                                                   student.StudentNumber,
                                                   student.IsActive,
                                                   student.User.FirstName,
                                                   student.User.LastName,
                                                   student.User.Email))
            .ToListAsync(cancellationToken);

        bool hasMoreItems = items.Count > normalizedPageSize;
        if (hasMoreItems)
            items.RemoveAt(items.Count - 1);

        return PagedResponse<List<StudentResponse>>.Create(items, normalizedPageSize, normalizedPage, hasMoreItems);
    }

    public async Task<List<Student>> GetByCenterIdAsync(int centerId, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(x => x.CenterId == centerId)
            .ToListAsync(cancellationToken);

    public async Task<List<Student>> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task<List<Student>> GetByCenterIdsAsync(IEnumerable<int> centerIds, CancellationToken cancellationToken = default)
    {
        var ids = centerIds.ToHashSet();
        return await Set
            .AsNoTracking()
            .Where(x => ids.Contains(x.CenterId))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> StudentExistsForUserAndCenterAsync(int userId, int centerId, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.CenterId == centerId, cancellationToken);

    public async Task<Student?> GetByUserIdAndCenterIdAsync(int userId, int centerId, CancellationToken cancellationToken = default)
        => await Set
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CenterId == centerId, cancellationToken);

}
