using Akay.Be.Domain.Enums;

namespace Akay.Be.Application.Features.Users;

/// <summary>
/// Filtros de negocio para listados paginados de usuarios.
/// </summary>
public sealed record UserListFilter(IReadOnlySet<int> AdminCenterIds,
                                    IReadOnlySet<int>? CenterIds,
                                    IReadOnlySet<UserRole>? Roles,
                                    string? Search,
                                    bool? IsActive);
