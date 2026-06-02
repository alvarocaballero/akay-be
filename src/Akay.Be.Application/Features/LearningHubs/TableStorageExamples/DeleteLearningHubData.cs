using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.TableStorage;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs.TableStorageExamples;

/// <summary>
/// Comando que elimina todos los datos de un Learning Hub en Table Storage
/// (metadatos y auditorias), verificando previamente si existen registros.
/// Demuestra <see cref="DeleteEntitiesByPartitionKeyAsync"/> y <see cref="ExistsPartitionKeyAsync"/>.
/// </summary>
public sealed record DeleteLearningHubDataCommand(int HubId) : ICommand<int>;

/// <summary>
/// Handler que elimina datos de Table Storage usando borrado por PartitionKey.
/// Verifica existencia previa con <see cref="ExistsPartitionKeyAsync"/> para informar
/// cuantos tipos de datos se van a eliminar.
/// </summary>
internal sealed class DeleteLearningHubDataCommandHandler(
    ITableStorageRepositoryFactory tableFactory) : ICommandHandler<DeleteLearningHubDataCommand, int>
{
    public async ValueTask<Result<int>> Handle(
        DeleteLearningHubDataCommand request,
        CancellationToken cancellationToken)
    {
        var partitionKey = $"hub-{request.HubId}";
        var auditRepo = tableFactory.Create("LearningHubAuditLogs");
        var metadataRepo = tableFactory.Create("LearningHubMetadata");

        var deletedCount = 0;

        // Verificar si existen metadatos antes de borrar
        var hasMetadata = await metadataRepo.ExistsPartitionKeyAsync(partitionKey, cancellationToken);
        if (hasMetadata.IsSuccess && hasMetadata.Value)
        {
            // DeleteEntitiesByPartitionKeyAsync:
            // - Borra todas las entidades de la particion
            // - Idempotente: si no hay entidades, devuelve exito
            // - Itera internamente en paginas de 1000 entidades
            var result = await metadataRepo.DeleteEntitiesByPartitionKeyAsync(
                partitionKey,
                cancellationToken);

            if (result.IsFailure)
                return Result<int>.Failure(result.Error);

            deletedCount++;
        }

        // Verificar si existen auditorias antes de borrar
        var hasAuditLogs = await auditRepo.ExistsPartitionKeyAsync(partitionKey, cancellationToken);
        if (hasAuditLogs.IsSuccess && hasAuditLogs.Value)
        {
            var result = await auditRepo.DeleteEntitiesByPartitionKeyAsync(
                partitionKey,
                cancellationToken);

            if (result.IsFailure)
                return Result<int>.Failure(result.Error);

            deletedCount++;
        }

        return deletedCount;
    }
}
