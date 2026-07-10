using Akay.Be.Domain.Enums;

namespace Akay.Be.Application.Features.UserRoles;

public sealed record UserRoleAssignmentResponse(int UserId,
                                                int? CenterId,
                                                UserRole Role);
