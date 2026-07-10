using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.AcademicPeriods;

public sealed record DeleteAcademicPeriodCommand(int Id) : ICommand;

internal sealed class DeleteAcademicPeriodCommandHandler(IAdminScopeService adminScope,
                                                         IUnitOfWork unitOfWork,
                                                         IAcademicPeriodRepository academicPeriodRepository) : ICommandHandler<DeleteAcademicPeriodCommand>
{
    public async ValueTask<Result> Handle(DeleteAcademicPeriodCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessAcademicPeriodAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var period = await academicPeriodRepository.GetByIdAsync(request.Id, cancellationToken);
        if (period is null)
            return Error.NotFound("academicperiod.not_found", $"Periodo académico {request.Id} no encontrado.");

        period.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
