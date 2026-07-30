namespace Akay.Be.Application.Features.Students;

/// <summary>
/// Filtros de negocio para listados paginados de estudiantes.
/// </summary>
public sealed record StudentListFilter(IReadOnlySet<int> CenterIds,
                                       string? Search,
                                       bool? IsActive);
