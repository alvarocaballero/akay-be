using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Notifications;
using Akay.Be.Infrastructure.SignalRHubs;
using Akay.To.Azure.Infrastructure.SignalR;

namespace Akay.Be.Infrastructure.Services;

internal sealed class DemoSignalRHubService(ISignalRHubService<DemoSignalRHub, DemoSignalRNotification> hubService) : IDemoSignalRHubService
{
    public async Task NotifyUserAsync(string userId, DemoSignalRNotification message) =>
        await hubService.SendUserAsync(userId, message);

    public async Task NotifyGroupAsync(string groupName, DemoSignalRNotification message) =>
        await hubService.SendGroupAsync(groupName, message);

    public async Task BroadcastMessageAsync(DemoSignalRNotification message) =>
        await hubService.BroadcastMessageAsync(message);
}
