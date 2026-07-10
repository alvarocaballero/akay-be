using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Courses;

public sealed record UpdateCourseCommand([property: JsonIgnore] int Id, string Name, string Code) : ICommand<CourseResponse>;

internal sealed class UpdateCourseCommandHandler(IAdminScopeService adminScope,
                                                 IUnitOfWork unitOfWork,
                                                 ICourseRepository courseRepository) : ICommandHandler<UpdateCourseCommand, CourseResponse>
{
    public async ValueTask<Result<CourseResponse>> Handle(UpdateCourseCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessCourseAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.Id, cancellationToken);
        if (course is null || course.DeletedAt is not null)
            return Error.NotFound("course.not_found", $"Curso {request.Id} no encontrado.");

        if (await courseRepository.CodeExistsInPeriodAsync(course.AcademicPeriodId, request.Code, request.Id, cancellationToken))
            return Error.Conflict("course.duplicate_code", "Ya existe un curso con ese codigo en el periodo academico.");

        course.UpdateName(request.Name);
        course.UpdateCode(request.Code);

        courseRepository.Update(course);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CourseMapper.ToResponse(course);
    }
}

public sealed class UpdateCourseCommandValidator : AbstractValidator<UpdateCourseCommand>
{
    public UpdateCourseCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
    }
}
