using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;


namespace Akay.Be.Application.Features.Subjects;

public sealed record AddSubjectAdminCommand([property: JsonIgnore] int SubjectId, int UserId) : ICommand<CreatedResponse<int>>;

internal sealed class AddSubjectAdminCommandHandler(IAdminScopeService adminScope,
                                                    IUnitOfWork unitOfWork,
                                                    ISubjectRepository subjectRepository) : ICommandHandler<AddSubjectAdminCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(AddSubjectAdminCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteSubjectAsync(request.SubjectId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var subject = await subjectRepository.GetWithCentersAsync(request.SubjectId, cancellationToken);
        if (subject is null)
            return Error.NotFound("subject.not_found", $"Asignatura {request.SubjectId} no encontrada.");

        var adminCenters = await adminScope.GetAdminCenterIdsAsync(cancellationToken);
        var subjectCenterIds = subject.Centers.Select(c => c.CenterId).ToHashSet();
        var manageableCenters = adminCenters.Intersect(subjectCenterIds).ToList();

        if (manageableCenters.Count == 0)
            return Error.Forbidden("admin.forbidden", "No tienes permisos para administrar esta asignatura.");

        var hasEligibleRole = await EligibleRoleInAnyCenterAsync(adminScope, request.UserId, manageableCenters, cancellationToken);
        if (!hasEligibleRole)
            return Error.Forbidden("subjectadmin.not_eligible", "El usuario debe tener rol Teacher o Admin en al menos uno de los centros gestionables de esta asignatura.");

        subject.AddAdmin(request.UserId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var admin = subject.Admins.First(a => a.UserId == request.UserId);
        return new CreatedResponse<int>(admin.SubjectId, admin.CreatedAt);
    }

    private static async Task<bool> EligibleRoleInAnyCenterAsync(IAdminScopeService adminScope, int userId, List<int> centerIds, CancellationToken cancellationToken)
    {
        foreach (var centerId in centerIds)
        {
            if (await adminScope.UserHasRoleInCenterAsync(userId, centerId, UserRole.Teacher, cancellationToken)
                || await adminScope.UserHasRoleInCenterAsync(userId, centerId, UserRole.Admin, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class AddSubjectAdminCommandValidator : AbstractValidator<AddSubjectAdminCommand>
{
    public AddSubjectAdminCommandValidator()
    {
        RuleFor(x => x.SubjectId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}
