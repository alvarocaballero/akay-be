using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.AcademicPeriods;

public sealed record CreateAcademicPeriodCommand([property: JsonIgnore] int CenterId, string Name, DateOnly StartDate, DateOnly EndDate) : ICommand<CreatedResponse<int>>;

internal sealed class CreateAcademicPeriodCommandHandler(IAdminScopeService adminScope,
                                                         IPersistenceResultExecutor persistence,
                                                         IAcademicPeriodRepository academicPeriodRepository) : ICommandHandler<CreateAcademicPeriodCommand, CreatedResponse<int>>
{
    private static readonly IReadOnlyDictionary<string, Error> KnownPersistenceErrors = new Dictionary<string, Error>
    {
        ["IX_AcademicPeriod_CenterId_Name"] = Error.Conflict("academicperiod.duplicate_name", "Ya existe un periodo académico con ese nombre en el centro."),
    };

    public async ValueTask<Result<CreatedResponse<int>>> Handle(CreateAcademicPeriodCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adminCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (adminCheck.IsFailure)
            return adminCheck.Error;

        if (await academicPeriodRepository.NameExistsInCenterAsync(request.CenterId, request.Name, cancellationToken: cancellationToken))
            return Error.Conflict("academicperiod.duplicate_name", "Ya existe un periodo académico con ese nombre en el centro.");

        var period = AcademicPeriod.Create(request.CenterId, request.Name, request.StartDate, request.EndDate);
        academicPeriodRepository.Add(period);

        var save = await persistence.TrySaveChangesAsync(KnownPersistenceErrors, cancellationToken);
        if (save.IsFailure)
            return save.Error;

        return new CreatedResponse<int>(period.Id, period.CreatedAt);
    }
}

public sealed class CreateAcademicPeriodCommandValidator : AbstractValidator<CreateAcademicPeriodCommand>
{
    public CreateAcademicPeriodCommandValidator()
    {
        RuleFor(x => x.CenterId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.StartDate).LessThan(x => x.EndDate);
    }
}
