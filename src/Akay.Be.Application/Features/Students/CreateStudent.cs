using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
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
                                                   ICourseRepository courseRepository) : ICommandHandler<CreateStudentCommand, CreatedResponse<int>>
{
    public async ValueTask<Result<CreatedResponse<int>>> Handle(CreateStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var centerCheck = await adminScope.EnsureAdminOfCenterAsync(request.CenterId, cancellationToken);
        if (centerCheck.IsFailure)
            return centerCheck.Error;

        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.FirstName)
            || string.IsNullOrWhiteSpace(request.LastName))
            return Error.Validation("user.missing_fields", "Email, FirstName y LastName son requeridos.");

        if (await userRepository.EmailExistsAsync(request.Email!, cancellationToken))
            return Error.Conflict("user.email_exists", "Ya existe un usuario con ese email.");

        var user = User.Create(request.Email!, request.FirstName!, request.LastName!);
        user.AssignRole(request.CenterId, UserRole.Student);
        var student = Domain.Entities.Academic.Student.Create(user, request.CenterId, request.StudentNumber);

        if (request.CourseId is > 0)
        {
            var courseAccess = await adminScope.EnsureCanWriteCourseAsync(request.CourseId.Value, cancellationToken);
            if (courseAccess.IsFailure)
                return courseAccess.Error;

            var course = await courseRepository.GetWithFullGraphAsync(request.CourseId.Value, cancellationToken: cancellationToken);
            if (course is null)
                return Error.NotFound("course.not_found", $"Curso {request.CourseId.Value} no encontrado.");

            if (course.AcademicPeriod.CenterId != request.CenterId)
                return Error.Forbidden("course.student_wrong_center", "El estudiante debe pertenecer al mismo centro que el curso.");

            course.EnrollStudent(student, request.SubjectIds);
        }

        userRepository.Add(user);
        studentRepository.Add(student);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreatedResponse<int>(student.UserId, student.CreatedAt);
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
