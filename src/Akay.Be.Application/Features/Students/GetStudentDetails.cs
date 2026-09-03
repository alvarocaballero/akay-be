using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.Students;

public sealed record GetStudentDetailsQuery(int UserId, int CenterId) : IQuery<StudentDetailResponse>;

internal sealed class GetStudentDetailsQueryHandler(IAdminScopeService adminScope,
                                                    IStudentRepository studentRepository) : IQueryHandler<GetStudentDetailsQuery, StudentDetailResponse>
{
    public async ValueTask<Result<StudentDetailResponse>> Handle(GetStudentDetailsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var access = await adminScope.EnsureCanAccessStudentAsync(request.UserId, request.CenterId, cancellationToken);
        if (access.IsFailure)
            return access.Error;

        var response = await studentRepository.GetStudentDetailsAsync(request.UserId, request.CenterId, cancellationToken);
        if (response is null)
            return Error.NotFound("student.not_found", $"Estudiante {request.UserId} no encontrado.");

        return response;
    }
}
