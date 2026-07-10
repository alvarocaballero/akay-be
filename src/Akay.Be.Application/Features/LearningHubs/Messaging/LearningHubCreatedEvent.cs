using Akay.To.Core.Application.Abstractions.Messaging;

namespace Akay.Be.Application.Features.LearningHubs.Messaging;

public sealed record LearningHubCreatedEvent(int Id,
                                             string Name,
                                             string Description) : IIntegrationEvent;
