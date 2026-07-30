using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.CourseStudents;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Students;

public sealed record CreateStudentCommand([property: JsonIgnore] int CenterId,
                                          string? StudentNumber = null,
                                          string? Email = null,
                                          string? FirstName = null,
                                          string? LastName = null,
                                          int? CourseId = null,
                                          int[]? SubjectIds = null) : ICommand<CreatedResponse<int>>;

internal sealed class CreateStudentCommandHandler(IAdminScopeService adminScope,
                                                  IUnitOfWork unitOfWork,
                                                  IStudentRepository studentRepository,
                                                  IUserRepository userRepository,
                                                  IDispatcher dispatcher) : ICommandHandler<CreateStudentCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(CreateStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var centerCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (centerCheck.IsFailure)
            return centerCheck.Error;

        var userIdResult = await ResolveOrCreateUserAsync(request, cancellationToken);
        if (userIdResult.IsFailure)
            return userIdResult.Error;

        var duplicate = await studentRepository.StudentExistsForUserAndCenterAsync(userIdResult.Value, request.CenterId, cancellationToken);
        if (duplicate)
            return Error.Conflict("student.duplicate", "Ya existe un perfil de estudiante para este usuario y centro.");

        var student = Domain.Entities.Academic.Student.Create(userIdResult.Value, request.CenterId, request.StudentNumber);
        studentRepository.Add(student);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (request.CourseId is > 0)
        {
            var enrollResult = await dispatcher.Send(new EnrollCourseStudentCommand(request.CourseId.Value,
                                                                                     student.Id,
                                                                                     request.SubjectIds),
                                                     cancellationToken);
            if (enrollResult.IsFailure)
                return enrollResult.Error;
        }

        return new CreatedResponse<int>(student.Id, student.CreatedAt);
    }

    private async ValueTask<Result<int>> ResolveOrCreateUserAsync(CreateStudentCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.FirstName)
            || string.IsNullOrWhiteSpace(request.LastName))
            return Error.Validation("user.missing_fields",
                                    "Email, FirstName y LastName son requeridos cuando no se proporciona UserId.");

        if (await userRepository.EmailExistsAsync(request.Email, ct))
            return Error.Conflict("user.email_exists", "Ya existe un usuario con ese email.");

        var newUser = User.Create(request.Email, request.FirstName, request.LastName);
        newUser.AssignRole(request.CenterId, UserRole.Student);
        userRepository.Add(newUser);
        await unitOfWork.SaveChangesAsync(ct);
        return newUser.Id;
    }
}

public sealed class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.CenterId).GreaterThan(0);
        RuleFor(x => x.StudentNumber).MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        When(x => x.CourseId.HasValue, () => RuleFor(x => x.CourseId!.Value).GreaterThan(0));

        When(x => x.SubjectIds is not null, () => RuleForEach(x => x.SubjectIds).GreaterThan(0));
    }
}
