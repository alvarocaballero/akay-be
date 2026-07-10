using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Students;

public sealed record GetStudentByIdQuery(int Id) : IQuery<StudentResponse>;

internal sealed class GetStudentByIdQueryHandler(IAdminScopeService adminScope,
                                                 IStudentRepository studentRepository) : IQueryHandler<GetStudentByIdQuery, StudentResponse>
{
    public async ValueTask<Result<StudentResponse>> Handle(GetStudentByIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessStudentAsync(request.Id, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var response = await studentRepository.GetByIdWithUserAsync(request.Id, cancellationToken);
        if (response is null)
            return Error.NotFound("student.not_found", $"Estudiante {request.Id} no encontrado.");

        return response;
    }
}
