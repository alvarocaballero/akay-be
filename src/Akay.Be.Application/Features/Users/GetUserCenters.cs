using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Users;

public sealed record GetUserCentersQuery : IQuery<IReadOnlyList<UserCenterResponse>>;

public sealed record UserCenterResponse(int Id, string Name);

internal sealed class GetUserCentersQueryHandler(IUserContext userContext,
                                                 IUserRepository userRepository) : IQueryHandler<GetUserCentersQuery, IReadOnlyList<UserCenterResponse>>
{
    public async ValueTask<Result<IReadOnlyList<UserCenterResponse>>> Handle(GetUserCentersQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var userId = userContext.UserId;
        if (userId <= 0)
            return new List<UserCenterResponse>();

        var centers = await userRepository.GetDistinctCentersByUserIdAsync(userId, cancellationToken);
        return centers
            .Select(c => new UserCenterResponse(c.Id, c.Name))
            .ToList();
    }
}
