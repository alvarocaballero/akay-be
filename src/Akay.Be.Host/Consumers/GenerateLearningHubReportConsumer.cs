using Akay.Be.Application.Features.LearningHubs.Messaging;
using Akay.To.Core.Application.Abstractions.Messaging;

namespace Akay.Be.Host.Consumers.Messaging;

/// <summary>
/// Consumer de ejemplo para mensajes point-to-point enviados con Rebus.
/// </summary>
internal sealed class GenerateLearningHubReportConsumer(ILogger<GenerateLearningHubReportConsumer> logger) : IMessageHandler<GenerateLearningHubReportMessage>
{
    /// <summary>
    /// Recibe el mensaje y deja un log de demostracion sin ejecutar logica adicional.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task HandleAsync(GenerateLearningHubReportMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Received GenerateLearningHubReportMessage for LearningHubId={Id}", message.Id);
        return Task.CompletedTask;
    }
}
