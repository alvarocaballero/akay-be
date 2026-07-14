using Akay.To.Core.Domain.Events;

namespace Akay.Be.Domain.Events.Academic;

public sealed record AcademicPeriodCreatedOutboxEvent(Guid SyncId,
                                                      int CenterId,
                                                      string Name,
                                                      DateOnly StartDate,
                                                      DateOnly EndDate,
                                                      bool IsActive) : IOutboxDomainEvent;
