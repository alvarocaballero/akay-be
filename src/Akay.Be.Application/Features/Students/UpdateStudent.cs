using System.Text.Json.Serialization;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Students;

public sealed record UpdateStudentCommand([property: JsonIgnore] int Id, string? StudentNumber, bool IsActive) : ICommand<StudentResponse>;

internal sealed class UpdateStudentCommandHandler(IAdminScopeService adminScope,
                                                  IUnitOfWork unitOfWork,
                                                  IStudentRepository studentRepository) : ICommandHandler<UpdateStudentCommand, StudentResponse>
{
    public async ValueTask<Result<StudentResponse>> Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanWriteStudentAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var student = await studentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (student is null || student.DeletedAt is not null)
            return Error.NotFound("student.not_found", $"Estudiante {request.Id} no encontrado.");

        student.ChangeStudentNumber(request.StudentNumber);
        if (request.IsActive)
            student.Activate();
        else
            student.Deactivate();

        studentRepository.Update(student);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new StudentResponse(student.Id, student.UserId, student.CenterId, student.StudentNumber, student.IsActive);
    }
}

public sealed class UpdateStudentCommandValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.StudentNumber).MaximumLength(50);
    }
}
