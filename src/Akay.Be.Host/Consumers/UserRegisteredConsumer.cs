using Akay.Be.Application.Features.LearningHubs.Messaging;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Messaging;
using Akay.To.Core.Infrastructure.Messaging;

namespace Akay.Be.Host.Consumers.Messaging;

/// <summary>
/// 
/// </summary>
/// <param name="logger"></param>
/// <param name="dispatcher"></param>
public sealed class UserRegisteredConsumer(ILogger<UserRegisteredConsumer> logger,
                                           IDispatcher dispatcher) : BaseConsumerToDispatcher(logger, dispatcher), IMessageHandler<LearningHubCreatedEvent>
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task HandleAsync(LearningHubCreatedEvent message,
                            CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Handling LearningHubCreatedEvent: Id={Id}, Name={Name}", message.Id, message.Name);

        return ConsumeAsCommand(message, static ev => new SendNewLearningHubNotification(ev.Id, ev.Name, ev.Description), cancellationToken);
    }
}
