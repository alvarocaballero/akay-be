using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.AcademicPeriods;

public sealed record UpdateAcademicPeriodCommand([property: JsonIgnore] int Id, string Name, DateOnly StartDate, DateOnly EndDate, bool IsActive) : ICommand<AcademicPeriodResponse>;

internal sealed class UpdateAcademicPeriodCommandHandler(IAdminScopeService adminScope,
                                                         IPersistenceResultExecutor persistence,
                                                         IAcademicPeriodRepository academicPeriodRepository) : ICommandHandler<UpdateAcademicPeriodCommand, AcademicPeriodResponse>
{
    private static readonly IReadOnlyDictionary<string, Error> KnownPersistenceErrors = new Dictionary<string, Error>
    {
        ["IX_AcademicPeriod_CenterId_Name"] = Error.Conflict("academicperiod.duplicate_name", "Ya existe un periodo académico con ese nombre en el centro."),
    };

    public async ValueTask<Result<AcademicPeriodResponse>> Handle(UpdateAcademicPeriodCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessAcademicPeriodAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var period = await academicPeriodRepository.GetByIdAsync(request.Id, cancellationToken);
        if (period is null || period.DeletedAt is not null)
            return Error.NotFound("academicperiod.not_found", $"Periodo académico {request.Id} no encontrado.");

        if (await academicPeriodRepository.NameExistsInCenterAsync(period.CenterId, request.Name, request.Id, cancellationToken))
            return Error.Conflict("academicperiod.duplicate_name", "Ya existe un periodo académico con ese nombre en el centro.");

        period.ChangeName(request.Name);
        period.ChangeDates(request.StartDate, request.EndDate);
        period.Deactivate();
        if (request.IsActive)
            period.Activate();

        academicPeriodRepository.Update(period);

        var save = await persistence.TrySaveChangesAsync(KnownPersistenceErrors, cancellationToken);
        if (save.IsFailure)
            return save.Error;

        return new AcademicPeriodResponse(period.Id, period.CenterId, period.Name, period.StartDate, period.EndDate, period.IsActive);
    }
}

public sealed class UpdateAcademicPeriodCommandValidator : AbstractValidator<UpdateAcademicPeriodCommand>
{
    public UpdateAcademicPeriodCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StartDate).LessThan(x => x.EndDate);
    }
}
