using Akay.Be.Domain.Events.Academic;
using Akay.To.Core.Application.Abstractions.Mediator;

namespace Akay.Be.Application.DomainEvents;

/// <summary>
/// Handler de ejemplo para <see cref="AcademicPeriodActivatedDomainEvent"/>.
/// Actualmente no realiza ninguna accion; sirve como referencia para aprender
/// como se estructura un manejador de eventos de dominio in-process.
/// </summary>
internal sealed class AcademicPeriodActivatedDomainEventHandler : IDomainEventHandler<AcademicPeriodActivatedDomainEvent>
{
    public ValueTask Handle(AcademicPeriodActivatedDomainEvent domainEvent,
                            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // TODO: implementar reaccion de negocio (enviar notificacion, recalcular estado, etc.)
        return ValueTask.CompletedTask;
    }
}
