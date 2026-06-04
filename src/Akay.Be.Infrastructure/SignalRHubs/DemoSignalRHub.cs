using Akay.Be.Application.Notifications;
using Akay.To.Azure.Infrastructure.Abstractions;
using Akay.To.Azure.Infrastructure.Hubs;

namespace Akay.Be.Infrastructure.SignalRHubs;

public class DemoSignalRHub : BaseHub<DemoSignalRHub, DemoSignalRNotification> // O heredar directamente de Hub si no se quieren los métodos y grupos predefinidos
{

    public DemoSignalRHub(ISignalRHubService<DemoSignalRHub, DemoSignalRNotification> hubService) : base(hubService) { }
}
