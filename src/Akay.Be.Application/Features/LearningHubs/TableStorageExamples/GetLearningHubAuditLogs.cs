using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.TableStorage;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs.TableStorageExamples;

/// <summary>
/// Consulta que recupera las entradas de auditoria de un Learning Hub desde Table Storage.
/// Los datos se almacenaron como <see cref="MemoArray"/> y se deserializan a <see cref="LearningHubAuditEntry"/>.
/// </summary>
public sealed record GetLearningHubAuditLogsQuery(int HubId) : IQuery<List<LearningHubAuditEntry>>;

/// <summary>
/// Handler que recupera auditorias de Table Storage mediante <see cref="GetObjectsByPartitionKeyAsync{TType}"/>.
/// Las entradas se guardaron con <see cref="UpsertObjectAsync{TValue}"/>, por lo que se recuperan
/// como <see cref="MemoArray"/> y se deserializan automaticamente a <typeparamref name="TType"/>.
/// </summary>
internal sealed class GetLearningHubAuditLogsQueryHandler(
    ITableStorageRepositoryFactory tableFactory) : IQueryHandler<GetLearningHubAuditLogsQuery, List<LearningHubAuditEntry>>
{
    public async ValueTask<Result<List<LearningHubAuditEntry>>> Handle(
        GetLearningHubAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        var repo = tableFactory.Create("LearningHubAuditLogs");

        // GetObjectsByPartitionKeyAsync:
        // 1. Obtiene los MemoArray de la particion "hub-{id}"
        // 2. Deserializa cada ObjectValue a LearningHubAuditEntry
        // 3. Si un MemoArray tiene ObjectValue nulo/vacio, devuelve error de validacion
        var result = await repo.GetObjectsByPartitionKeyAsync<LearningHubAuditEntry>(
            $"hub-{request.HubId}",
            cancellationToken);

        if (result.IsFailure)
            return Result<List<LearningHubAuditEntry>>.Failure(result.Error);

        // Los registros ya vienen en orden cronologico inverso gracias a TicksDesc
        return result.Value?.ToList() ?? [];
    }
}
