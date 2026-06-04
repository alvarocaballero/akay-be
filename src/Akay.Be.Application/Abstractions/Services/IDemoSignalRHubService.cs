using Akay.Be.Application.Notifications;

namespace Akay.Be.Application.Abstractions.Services;

public interface IDemoSignalRHubService
{
    Task NotifyUserAsync(string userId, DemoSignalRNotification message);
    Task NotifyGroupAsync(string groupName, DemoSignalRNotification message);
    Task BroadcastMessageAsync(DemoSignalRNotification message);
}
