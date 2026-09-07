using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Users;

public sealed record DeleteUserCommand(int Id) : ICommand;

internal sealed class DeleteUserCommandHandler(IAdminScopeService adminScope,
                                               IUnitOfWork unitOfWork,
                                               IUserRepository userRepository,
                                               IStudentRepository studentRepository,
                                               ICourseRepository courseRepository) : ICommandHandler<DeleteUserCommand>
{
    public async ValueTask<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteUserAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var user = await userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user is null)
            return UserErrors.NotFound(request.Id);

        user.Deactivate();

        //TODO: Outbox event. Implement worker to cleanup external identity
        user.RequestExternalIdentityCleanup();
        userRepository.Remove(user);
        studentRepository.RemoveRange(await studentRepository.GetByUserIdForUpdateAsync(user.Id, cancellationToken));

        await courseRepository.UnenrollStudentAsync(user.Id, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
