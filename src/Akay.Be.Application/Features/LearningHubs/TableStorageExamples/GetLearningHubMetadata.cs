using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.TableStorage;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs.TableStorageExamples;

/// <summary>
/// Registro plano de metadatos devuelto por la consulta paginada de Table Storage.
/// Las propiedades se mapean desde las columnas de la entidad de tabla.
/// </summary>
public sealed record LearningHubMetadataRow(
    int? TotalStudents,
    int? TotalCourses,
    double? AverageRating,
    string? Tags)
{
    public LearningHubMetadataRow() : this(null, null, null, null) { }
}

/// <summary>
/// Consulta paginada que recupera metadatos de Learning Hubs usando <see cref="QueryAsync{TEntity}"/>
/// con un filtro estructurado <see cref="TableStorageFilter"/>.
/// </summary>
public sealed record GetLearningHubMetadataQuery(
    int HubId,
    int PageSize = 5,
    string? ContinuationToken = null) : IQuery<Result<(List<LearningHubMetadataRow> Results, string? NextToken)>>;

/// <summary>
/// Handler que consulta metadatos en Table Storage usando <see cref="QueryAsync{TEntity}"/>
/// con filtro estructurado y paginacion. Demuestra el uso de <see cref="TableStorageFilter"/>
/// como alternativa segura a concatenar strings OData manualmente.
/// </summary>
internal sealed class GetLearningHubMetadataQueryHandler(
    ITableStorageRepositoryFactory tableFactory) : IQueryHandler<GetLearningHubMetadataQuery, Result<(List<LearningHubMetadataRow>, string?)>>
{
    public async ValueTask<Result<Result<(List<LearningHubMetadataRow>, string?)>>> Handle(
        GetLearningHubMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var repo = tableFactory.Create("LearningHubMetadata");

        // Construir filtro estructurado con TableStorageFilter.
        // Evita concatenacion manual de strings OData y posibles inyecciones.
        // El filtro resultante sera: PartitionKey eq 'hub-{id}'
        var filter = TableStorageFilter
            .PartitionKey($"hub-{request.HubId}");

        // QueryAsync:
        // - Aplica el filtro OData generado por TableStorageFilterODataBuilder
        // - Pagina los resultados segun pageSize
        // - Devuelve la lista de entidades y el token para la pagina siguiente
        var queryResult = await repo.QueryAsync<LearningHubMetadataRow>(
            filter,
            request.PageSize,
            request.ContinuationToken,
            cancellationToken);

        if (queryResult.IsFailure)
            return Result<Result<(List<LearningHubMetadataRow>, string?)>>.Failure(queryResult.Error);

        return Result<Result<(List<LearningHubMetadataRow>, string?)>>.Success(queryResult);
    }
}
