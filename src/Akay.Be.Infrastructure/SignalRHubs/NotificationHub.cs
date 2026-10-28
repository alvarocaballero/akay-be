using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Akay.Be.Infrastructure.SignalRHubs;

/// <summary>Receive-only hub: clients cannot publish notifications or select other users' groups.</summary>
[Authorize]
public sealed class NotificationHub : Hub;
