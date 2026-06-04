using System.Globalization;
using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

namespace Akay.Be.Application.Features.LearningHubs.SignalRExample;

public record DemoSignalRSendCommand(int Id) : ICommand;


public class DemoSignalRSendHandler(IDemoSignalRHubService service,
                                    IUserContext userContext) : ICommandHandler<DemoSignalRSendCommand>
{
    public async ValueTask<Result> Handle(DemoSignalRSendCommand request, CancellationToken cancellationToken)
    {
        var userId = userContext.UserId.ToString(CultureInfo.InvariantCulture);

        for (int x = 0; x < 5; x++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userMessage  = new Notifications.DemoSignalRNotification(request.Id, $"Mensaje directo (#{x + 1})", DateTime.UtcNow);
            var autoMessage  = new Notifications.DemoSignalRNotification(request.Id, $"Grupo automatico user-{userId} (#{x + 1})", DateTime.UtcNow);
            var demoMessage  = new Notifications.DemoSignalRNotification(request.Id, $"Grupo manual DemoGroup (#{x + 1})", DateTime.UtcNow);
            var broadMessage = new Notifications.DemoSignalRNotification(request.Id, $"Broadcast to everyone (#{x + 1})", DateTime.UtcNow);

            await service.NotifyUserAsync(userId, userMessage);
            await service.NotifyGroupAsync($"user-{userId}", autoMessage);
            await service.NotifyGroupAsync("DemoGroup", demoMessage);
            await service.BroadcastMessageAsync(broadMessage);

            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
        }

        return Result.Success();
    }
}
