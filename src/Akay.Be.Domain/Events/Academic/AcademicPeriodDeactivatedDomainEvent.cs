using Akay.To.Core.Domain.Events;

namespace Akay.Be.Domain.Events.Academic;

/// <summary>
/// Evento de dominio interno que indica que un periodo academico ha sido activado.
/// No es un <see cref="IOutboxDomainEvent"/>: se procesa in-process por un handler
/// y no se serializa en la tabla de Outbox.
/// </summary>
public sealed record AcademicPeriodDeactivatedDomainEvent(Guid SyncId,
                                                          int CenterId,
                                                          string Name) : IDomainEvent;
