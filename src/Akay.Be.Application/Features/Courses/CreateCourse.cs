using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Courses;

public sealed record CreateCourseCommand(int AcademicPeriodId, string Name, string Code) : ICommand<CreatedResponse<int>>;

internal sealed class CreateCourseCommandHandler(IAdminScopeService adminScope,
                                                 IUnitOfWork unitOfWork,
                                                 ICourseRepository courseRepository,
                                                 IAcademicPeriodRepository academicPeriodRepository) : ICommandHandler<CreateCourseCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteAcademicPeriodAsync(request.AcademicPeriodId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var period = await academicPeriodRepository.GetByIdAsync(request.AcademicPeriodId, cancellationToken);
        if (period is null || period.DeletedAt is not null)
            return Error.NotFound("academicperiod.not_found", $"Periodo academico {request.AcademicPeriodId} no encontrado.");

        if (await courseRepository.CodeExistsInPeriodAsync(request.AcademicPeriodId, request.Code, cancellationToken: cancellationToken))
            return Error.Conflict("course.duplicate_code", "Ya existe un curso con ese codigo en el periodo academico.");

        var course = Domain.Entities.Academic.Course.Create(request.AcademicPeriodId, request.Name, request.Code);
        courseRepository.Add(course);

        var save = await unitOfWork.ResultSaveChangesAsync(cancellationToken);
        if (save.IsFailure)
            return save.Error;

        return new CreatedResponse<int>(course.Id, course.CreatedAt);
    }
}

public sealed class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
{
    public CreateCourseCommandValidator()
    {
        RuleFor(x => x.AcademicPeriodId).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}
