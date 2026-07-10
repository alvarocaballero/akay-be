using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Students;

public sealed record CreateStudentCommand(int UserId, int CenterId, string? StudentNumber = null) : ICommand<CreatedResponse<int>>;

internal sealed class CreateStudentCommandHandler(IAdminScopeService adminScope,
                                                  IUnitOfWork unitOfWork,
                                                  IStudentRepository studentRepository,
                                                  IUserRepository userRepository) : ICommandHandler<CreateStudentCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(CreateStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var centerCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (centerCheck.IsFailure)
            return centerCheck.Error;

        var existing = await studentRepository.StudentExistsForUserAndCenterAsync(request.UserId, request.CenterId, cancellationToken);
        if (existing)
            return Error.Conflict("student.duplicate", "Ya existe un perfil de estudiante para este usuario y centro.");

        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Error.NotFound("user.not_found", $"Usuario {request.UserId} no encontrado.");

        if (!user.RoleAssignments.Any(r => r.CenterId == request.CenterId && r.Role == UserRole.Student))
            user.AssignRole(request.CenterId, UserRole.Student);

        var student = Domain.Entities.Academic.Student.Create(request.UserId, request.CenterId, request.StudentNumber);
        studentRepository.Add(student);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(student.Id, student.CreatedAt);
    }
}

public sealed class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.CenterId).GreaterThan(0);
        RuleFor(x => x.StudentNumber).MaximumLength(50);
    }
}
