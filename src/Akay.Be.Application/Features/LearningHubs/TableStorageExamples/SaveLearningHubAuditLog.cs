using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.TableStorage;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.LearningHubs.TableStorageExamples;

/// <summary>
/// Entidad de auditoria que se guarda como <see cref="MemoArray"/> (objeto completo serializado).
/// Al ser un record serializable con <c>System.Text.Json</c>, <see cref="UpsertObjectAsync{TValue}"/>
/// lo guarda en una unica columna <c>ObjectValue</c> junto con su <c>AssemblyQualifiedName</c> en <c>ObjectType</c>,
/// permitiendo recuperarlo y deserializarlo al tipo original en tiempo de ejecucion.
/// </summary>
public sealed record LearningHubAuditEntry(
    DateTimeOffset Timestamp,
    int HubId,
    string Action,
    string? Details);

/// <summary>
/// Comando que registra una entrada de auditoria para un Learning Hub.
/// Usa almacenamiento basado en <see cref="MemoArray"/> (objeto serializado).
/// </summary>
public sealed record SaveLearningHubAuditLogCommand(
    int HubId,
    string Action,
    string? Details) : ICommand<LearningHubAuditEntry>;

/// <summary>
/// Handler que guarda una entrada de auditoria en Table Storage mediante <see cref="UpsertObjectAsync{TValue}"/>.
/// Usa <see cref="RowKeyType.TicksDesc"/> para que las entradas mas recientes aparezcan primero
/// en consultas sin filtro.
/// </summary>
internal sealed class SaveLearningHubAuditLogCommandHandler(
    ITableStorageRepositoryFactory tableFactory) : ICommandHandler<SaveLearningHubAuditLogCommand, LearningHubAuditEntry>
{
    public async ValueTask<Result<LearningHubAuditEntry>> Handle(
        SaveLearningHubAuditLogCommand request,
        CancellationToken cancellationToken)
    {
        // Crear repositorio vinculado a la tabla "LearningHubAuditLogs".
        // forceCreateTable: true => crea la tabla si no existe.
        var repo = tableFactory.Create("LearningHubAuditLogs", forceCreateTable: true);

        var entry = new LearningHubAuditEntry(
            Timestamp: DateTimeOffset.UtcNow,
            HubId: request.HubId,
            Action: request.Action,
            Details: request.Details);

        var partitionKey = $"hub-{request.HubId}";

        // UpsertObjectAsync serializa el objeto completo a JSON y lo guarda como MemoArray:
        // - ObjectType  = AssemblyQualifiedName de LearningHubAuditEntry
        // - ObjectValue = JSON del objeto
        // RowKeyType.TicksDesc genera una RowKey decremental basada en UTC,
        // invirtiendo el orden natural para que las entradas mas recientes aparezcan primero.
        var result = await repo.UpsertObjectAsync(
            partitionKey,
            RowKeyType.TicksDesc,
            entry,
            updateMode: UpdateMode.Merge,
            cancellationToken: cancellationToken);

        if (result.IsFailure)
            return Result<LearningHubAuditEntry>.Failure(result.Error);

        return entry;
    }
}

/// <summary>
/// Validador para <see cref="SaveLearningHubAuditLogCommand"/>.
/// </summary>
public sealed class SaveLearningHubAuditLogCommandValidator : AbstractValidator<SaveLearningHubAuditLogCommand>
{
    public SaveLearningHubAuditLogCommandValidator()
    {
        RuleFor(x => x.HubId).GreaterThan(0);
        RuleFor(x => x.Action).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Details).MaximumLength(500);
    }
}
