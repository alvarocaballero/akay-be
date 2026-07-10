namespace Akay.Be.Application.Features.Students;

public sealed record StudentResponse(int Id,
                                     int UserId,
                                     int CenterId,
                                     string? StudentNumber,
                                     bool IsActive,
                                     string? FirstName = null,
                                     string? LastName = null,
                                     string? Email = null);
