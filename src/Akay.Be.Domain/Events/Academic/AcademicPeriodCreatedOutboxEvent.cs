using Akay.To.Core.Domain.Events;

namespace Akay.Be.Domain.Events.Academic;

/// <summary>
/// Hecho de negocio: un periodo academico fue creado. Se persiste en el outbox para que otros
/// componentes puedan reaccionar y se publica a los suscriptores interesados.
/// </summary>
public sealed record AcademicPeriodCreatedOutboxEvent(Guid SyncId,
                                                      int CenterId,
                                                      string Name,
                                                      DateOnly StartDate,
                                                      DateOnly EndDate,
                                                      bool IsActive) : IOutboxDomainEvent; // IOutboxIntegrationEvent cuando se procesen
