using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Courses;

public sealed record DeleteCourseCommand(int Id) : ICommand;

internal sealed class DeleteCourseCommandHandler(IAdminScopeService adminScope,
                                                 IUnitOfWork unitOfWork,
                                                 ICourseRepository courseRepository) : ICommandHandler<DeleteCourseCommand>
{
    public async ValueTask<Result> Handle(DeleteCourseCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteCourseAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var course = await courseRepository.GetByIdAsync(request.Id, cancellationToken);
        if (course is null || course.DeletedAt is not null)
            return Error.NotFound("course.not_found", $"Curso {request.Id} no encontrado.");

        course.SoftDelete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
