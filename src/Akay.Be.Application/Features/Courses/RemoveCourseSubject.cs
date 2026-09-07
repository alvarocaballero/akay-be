using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Courses;

public sealed record RemoveCourseSubjectCommand(int CourseId, int SubjectId) : ICommand<CourseResponse>;

internal sealed class RemoveCourseSubjectCommandHandler(IAdminScopeService adminScope,
                                                        IUnitOfWork unitOfWork,
                                                        ICourseRepository courseRepository) : ICommandHandler<RemoveCourseSubjectCommand, CourseResponse>
{
    public async ValueTask<Result<CourseResponse>> Handle(RemoveCourseSubjectCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteCourseAsync(request.CourseId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetWithFullGraphAsync(request.CourseId, cancellationToken: cancellationToken);
        if (course is null)
            return Error.NotFound("course.not_found", $"Curso {request.CourseId} no encontrado.");

        var courseSubject = course.Subjects.FirstOrDefault(subject => subject.SubjectId == request.SubjectId);
        if (courseSubject is null)
            return Error.NotFound("course.subject_not_found", $"La asignatura {request.SubjectId} no está asignada a este curso.");

        courseRepository.Remove(courseSubject);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CourseMapper.ToResponse(course);
    }
}

public sealed class RemoveCourseSubjectCommandValidator : AbstractValidator<RemoveCourseSubjectCommand>
{
    public RemoveCourseSubjectCommandValidator()
    {
        RuleFor(x => x.CourseId).GreaterThan(0);
        RuleFor(x => x.SubjectId).GreaterThan(0);
    }
}
