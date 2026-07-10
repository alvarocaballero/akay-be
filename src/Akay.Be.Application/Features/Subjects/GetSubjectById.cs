using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Subjects;

public sealed record GetSubjectByIdQuery(int Id) : IQuery<SubjectResponse>;

internal sealed class GetSubjectByIdQueryHandler(IAdminScopeService adminScope,
                                                 ISubjectRepository subjectRepository,
                                                 IUserRepository userRepository) : IQueryHandler<GetSubjectByIdQuery, SubjectResponse>
{
    public async ValueTask<Result<SubjectResponse>> Handle(GetSubjectByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessSubjectAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var subject = await subjectRepository.GetWithCentersAsync(request.Id, cancellationToken);
        if (subject is null)
            return Error.NotFound("subject.not_found", $"Asignatura {request.Id} no encontrada.");

        var adminUserIds = subject.Admins.Select(a => a.UserId).ToList();
        var adminUsers = adminUserIds.Count > 0
            ? await userRepository.GetByIdsAsync(adminUserIds, cancellationToken)
            : [];

        return new SubjectResponse(subject.Id,
                                   subject.Name,
                                   subject.Description,
                                   subject.Centers.Select(c => c.CenterId).ToList())
        {
            AdminUsers = adminUsers
                .Select(u => new AdminUserResponse(u.Id, u.FirstName, u.LastName, u.Email))
                .ToList()
        };
    }
}
