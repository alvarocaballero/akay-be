using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Students;

public sealed record GetStudentByUserIdQuery(int UserId, int CenterId) : IQuery<StudentResponse>;

internal sealed class GetStudentByUserIdQueryHandler(IAdminScopeService adminScope,
                                                     IStudentRepository studentRepository) : IQueryHandler<GetStudentByUserIdQuery, StudentResponse>
{
    public async ValueTask<Result<StudentResponse>> Handle(GetStudentByUserIdQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessStudentAsync(request.UserId, request.CenterId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var response = await studentRepository.GetByUserIdAndCenterIdWithUserAsync(request.UserId, request.CenterId, cancellationToken);
        if (response is null)
            return Error.NotFound("student.not_found", $"Estudiante {request.UserId} no encontrado en el centro {request.CenterId}.");

        return response;
    }
}
