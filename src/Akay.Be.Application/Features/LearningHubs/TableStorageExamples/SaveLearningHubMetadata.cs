using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.TableStorage;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs.TableStorageExamples;

/// <summary>
/// Datos de metadatos guardados como entidad de tabla (columnas individuales).
/// A diferencia de <see cref="MemoArray"/>, cada propiedad se mapea a una columna de Table Storage,
/// permitiendo filtrar y consultar por estos campos directamente.
/// </summary>
public sealed record LearningHubMetadataResponse(
    int HubId,
    int TotalStudents,
    int TotalCourses,
    double AverageRating,
    string? Tags,
    string RowKey,
    DateTime Timestamp);

/// <summary>
/// Comando que persiste metadatos de un Learning Hub como entidad de tabla (propiedades mapeadas a columnas).
/// Util cuando se necesita filtrar por campos individuales (ej. rating > 4.0)
/// o consultar solo ciertas columnas sin deserializar el objeto completo.
/// </summary>
public sealed record SaveLearningHubMetadataCommand(
    int HubId,
    int TotalStudents,
    int TotalCourses,
    double AverageRating,
    string? Tags) : ICommand<LearningHubMetadataResponse>;

/// <summary>
/// Handler que guarda metadatos en Table Storage mediante <see cref="UpsertAsync{TType}"/>.
/// Las propiedades publicas de <typeparamref name="TType"/> se mapean automaticamente a columnas
/// de la entidad de tabla, permitiendo consultas OData sobre campos individuales.
/// </summary>
internal sealed class SaveLearningHubMetadataCommandHandler(
    ITableStorageRepositoryFactory tableFactory) : ICommandHandler<SaveLearningHubMetadataCommand, LearningHubMetadataResponse>
{
    public async ValueTask<Result<LearningHubMetadataResponse>> Handle(
        SaveLearningHubMetadataCommand request,
        CancellationToken cancellationToken)
    {
        var repo = tableFactory.Create("LearningHubMetadata", forceCreateTable: true);

        // UpsertAsync mapea cada propiedad publica a una columna de TableEntity:
        // - PartitionKey = "hub-{id}"
        // - RowKey = generada con TicksDesc (orden cronologico inverso)
        // - TotalStudents, TotalCourses, AverageRating, Tags -> columnas individuales
        // - UpdateMode.Merge: fusiona con entidad existente, conservando columnas no incluidas
        var metadata = new
        {
            request.TotalStudents,
            request.TotalCourses,
            request.AverageRating,
            request.Tags
        };

        var result = await repo.UpsertAsync(
            $"hub-{request.HubId}",
            RowKeyType.TicksDesc,
            metadata,
            updateMode: UpdateMode.Merge,
            cancellationToken: cancellationToken);

        if (result.IsFailure)
            return Result<LearningHubMetadataResponse>.Failure(result.Error);

        return new LearningHubMetadataResponse(
            request.HubId,
            request.TotalStudents,
            request.TotalCourses,
            request.AverageRating,
            request.Tags,
            RowKey: "ver-consulta-GetLearningHubMetadata",
            Timestamp: DateTime.UtcNow);
    }
}
