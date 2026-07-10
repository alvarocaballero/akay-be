namespace Akay.Be.Application.Features.Users;

public sealed record UserResponse(int Id,
                                  Guid? ExternalId,
                                  string Email,
                                  string FirstName,
                                  string LastName,
                                  bool IsActive);

public sealed record UserListItemResponse(int Id,
                                          Guid? ExternalId,
                                          string Email,
                                          string FirstName,
                                          string LastName,
                                          bool IsActive);

public sealed record UserWithRolesResponse(int Id,
                                           Guid? ExternalId,
                                           string Email,
                                           string FirstName,
                                           string LastName,
                                           bool IsActive,
                                           IReadOnlyList<UserCenterRoleResponse> Roles);

public sealed record UserCenterRoleResponse(int CenterId, string Role);
