using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Courses;

public sealed record AddCourseSubjectCommand(int CourseId, int SubjectId) : ICommand<CreatedResponse<int>>;

internal sealed class AddCourseSubjectCommandHandler(IAdminScopeService adminScope,
                                                     IUnitOfWork unitOfWork,
                                                     ICourseRepository courseRepository,
                                                     ISubjectRepository subjectRepository) : ICommandHandler<AddCourseSubjectCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(AddCourseSubjectCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessCourseAsync(request.CourseId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.CourseId, cancellationToken);
        if (course is null || course.DeletedAt is not null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        var centerId = course.AcademicPeriod.CenterId;
        var subjectAvailable = await subjectRepository.SubjectIsAvailableForCenterAsync(request.SubjectId, centerId, cancellationToken);
        if (!subjectAvailable)
            return Error.Forbidden("course.subject_not_available", "La asignatura no está disponible para el centro del curso.");

        course.AddSubject(request.SubjectId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(course.Id, course.CreatedAt);
    }
}

public sealed class AddCourseSubjectCommandValidator : AbstractValidator<AddCourseSubjectCommand>
{
    public AddCourseSubjectCommandValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.SubjectId).GreaterThan(0);
    }
}
