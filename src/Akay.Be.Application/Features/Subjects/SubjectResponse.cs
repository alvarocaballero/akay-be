namespace Akay.Be.Application.Features.Subjects;

public sealed record SubjectResponse(int Id,
                                     string Name,
                                     string? Description,
                                     IReadOnlyList<int> CenterIds,
                                     bool IsActive)
{
    public IReadOnlyList<AdminUserResponse> AdminUsers { get; init; } = [];
}

public sealed record AdminUserResponse(int UserId,
                                       string FirstName,
                                       string LastName,
                                       string Email);
