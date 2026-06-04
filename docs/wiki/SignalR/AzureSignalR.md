# Azure SignalR

## Que es

`ISignalRHubService<THub, TMessage>` es una abstraccion generica sobre `Microsoft.AspNetCore.SignalR` que proporciona envio de mensajes con tres capas por dominio: DTO tipado del Hub (`TMessage`), payload generico (`<T>`) y texto plano (`*Text*`). Soporta broadcast, usuarios individuales y grupos, con modo dual transparente: Azure SignalR Service en produccion y SignalR local en desarrollo.

Cada instancia del servicio esta ligada a un `Hub` concreto y un tipo de mensaje especifico, garantizando type safety en todas las operaciones. `BaseHub<THub, TMessage>` ofrece bateria incluida con `[Authorize]`, grupos automaticos por usuario y metodos de envio invocables desde frontend listos para usar.

**Paquete:** `Akay.To.Azure`
**Interfaz:** `Akay.To.Azure.Infrastructure.Abstractions.ISignalRHubService<THub, TMessage>`
**Implementacion:** `Akay.To.Azure.Infrastructure.Services.AzureSignalRHubService<THub, TMessage>`
**Registro DI:** `Akay.To.Azure.Infrastructure.DependencyInjection.SignalRConfiguration`

---

## Por que usarlo

- **API en tres capas:** cada operacion expone tres variantes — el DTO tipado del Hub (`TMessage`) como ruta principal, un payload generico (`<T>`) para casos atipicos, y texto plano (`*Text*`) para demos, mensajes simples y envios desde frontend.
- **Modo dual transparente:** si `AzureSignalRSettings.ConnectionString` esta configurado, se usa Azure SignalR Service (scale-out). Si no, se usa SignalR local en memoria. La aplicacion no necesita saber que modo esta activo.
- **Tipado generico:** `ISignalRHubService<THub, TMessage>` vincula el hub y el tipo de mensaje en tiempo de compilacion. No hay casts ni `object` en las llamadas.
- **Autenticacion integrada:** `BaseHub<THub, TMessage>` incluye el atributo `[Authorize]`. Todos los hubs que hereden de el requieren autenticacion automaticamente.
- **Bateria incluida en BaseHub:** heredar de `BaseHub<THub, TMessage>` proporciona `JoinGroup`, `LeaveGroup`, `SendToAll`, `SendToUser`, `SendToUsers`, `SendToGroup`, `SendToGroups`, `SendToGroupExcept`, `SendToCaller`, `SendToOthers` y `SendToOthersInGroup` listos para invocar desde frontend sin escribir codigo.
- **Separacion de responsabilidades:** los metodos de `BaseHub` son invocables desde el frontend (cliente SignalR). `ISignalRHubService` expone metodos para el backend (controladores, handlers, workers).
- **Sender name rastreable:** cada mensaje incluye un parametro `name` que identifica al emisor. Por defecto es `_SYSTEM_` en el backend y `Context.UserIdentifier` en el frontend.
- **Fail-safe:** si la configuracion de Azure SignalR falta o es nula, el sistema arranca con SignalR local. No hay excepciones de inicio.
- **Registro Transient:** el servicio generico es stateless, lo que permite usarlo en contextos Scoped y Singleton.
- **Constantes centralizadas:** `SignalRConstants` define `EchoEvent` y `BroadcastEvent`, usados tanto por `AzureSignalRHubService` como por `BaseHub`, garantizando consistencia entre servidor y frontend.

---

## Arquitectura

### ISignalRHubService<THub, TMessage>

```csharp
public interface ISignalRHubService<THub, TMessage>
    where THub : Hub
    where TMessage : class
{
    protected const string DefaultSenderName = "_SYSTEM_";

    // -- Broadcast --
    Task BroadcastMessageAsync(TMessage message, string? name = DefaultSenderName);
    Task BroadcastMessageAsync<T>(T message, string? name = DefaultSenderName) where T : class;
    Task BroadcastTextAsync(string message, string? name = DefaultSenderName);

    // -- User --
    Task SendUserAsync(string userId, TMessage message, string? name = DefaultSenderName);
    Task SendUserAsync<T>(string userId, T message, string? name = DefaultSenderName) where T : class;
    Task SendUserTextAsync(string userId, string message, string? name = DefaultSenderName);

    Task SendUsersAsync(IReadOnlyList<string> userIds, TMessage message, string? name = DefaultSenderName);
    Task SendUsersAsync<T>(IReadOnlyList<string> userIds, T message, string? name = DefaultSenderName) where T : class;
    Task SendUsersTextAsync(IReadOnlyList<string> userIds, string message, string? name = DefaultSenderName);

    // -- Group --
    Task SendGroupAsync(string groupName, TMessage message, string? name = DefaultSenderName);
    Task SendGroupAsync<T>(string groupName, T message, string? name = DefaultSenderName) where T : class;
    Task SendGroupTextAsync(string groupName, string message, string? name = DefaultSenderName);

    Task SendGroupsAsync(IReadOnlyList<string> groupNames, TMessage message, string? name = DefaultSenderName);
    Task SendGroupsAsync<T>(IReadOnlyList<string> groupNames, T message, string? name = DefaultSenderName) where T : class;
    Task SendGroupsTextAsync(IReadOnlyList<string> groupNames, string message, string? name = DefaultSenderName);

    Task SendGroupExceptAsync(string groupName, IReadOnlyList<string> connectionIdExcept, TMessage message, string? name = DefaultSenderName);
    Task SendGroupExceptAsync<T>(string groupName, IReadOnlyList<string> connectionIdExcept, T message, string? name = DefaultSenderName) where T : class;
    Task SendGroupExceptTextAsync(string groupName, IReadOnlyList<string> connectionIdExcept, string message, string? name = DefaultSenderName);

    // -- Group Management --
    Task JoinConnectionToGroupAsync(string connectionId, string groupName);
    Task LeaveConnectionFromGroupAsync(string connectionId, string groupName);
}
```

### SignalRConstants

```csharp
public static class SignalRConstants
{
    public const string EchoEvent = "echo";
    public const string BroadcastEvent = "broadcastMessage";
}
```

### BaseHub<THub, TMessage>

```csharp
[Authorize]
public abstract class BaseHub<THub, TMessage>(ISignalRHubService<THub, TMessage> hubService) : Hub
    where THub : Hub
    where TMessage : class
{
    private const string AnonymousUser = "anonymous";

    public override async Task OnConnectedAsync()
    {
        // Auto-join a grupo por usuario (comentado por defecto, descomentar si se necesita):
        // var userId = Context.UserIdentifier;
        // if (!string.IsNullOrWhiteSpace(userId))
        //     await hubService.JoinConnectionToGroupAsync(Context.ConnectionId, $"user-{userId}");

        await base.OnConnectedAsync();
    }

    public virtual async Task JoinGroup(string groupName) => ...;
    public virtual async Task LeaveGroup(string groupName) => ...;
    public virtual async Task SendToAll(string message) => ...;
    public virtual async Task SendToUser(string userId, string message) => ...;
    public virtual async Task SendToUsers(IReadOnlyList<string> userIds, string message) => ...;
    public virtual async Task SendToGroup(string groupName, string message) => ...;
    public virtual async Task SendToGroups(IReadOnlyList<string> groupNames, string message) => ...;
    public virtual async Task SendToGroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds, string message) => ...;
    public virtual async Task SendToCaller(string message) => ...;
    public virtual async Task SendToOthers(string message) => ...;
    public virtual async Task SendToOthersInGroup(string groupName, string message) => ...;
}
```

Los metodos de `BaseHub` usan internamente `BroadcastTextAsync`, `SendUserTextAsync`, `SendGroupTextAsync`, etc. del servicio generico, y usan `SignalRConstants.EchoEvent` / `SignalRConstants.BroadcastEvent` para los nombres de evento en el cliente.

### AzureSignalRSettings

```csharp
public class AzureSignalRSettings
{
    public string? ConnectionString { get; set; }
}
```

### DemoSignalRNotification (ejemplo de DTO)

```csharp
public record DemoSignalRNotification(int? Id, string Message, DateTime Timestamp);
```

---

## Configuracion

### Registro en DI

```csharp
using Akay.To.Azure.Infrastructure.DependencyInjection;

services.AddSignalR(settings?.AzureSignalRSettings);
```

### Que registra

| Servicio | Lifetime |
|---|---|
| `ISignalRHubService<,>` (generico abierto) | Transient |
| `AzureSignalRHubService<,>` (implementacion) | Transient |
| SignalR server (`.AddSignalR()` o `.AddSignalR().AddAzureSignalR(...)`) | Singleton |

El hub concreto se registra aparte via `MapHub`:

```csharp
app.MapHub<DemoSignalRHub>("/hub/demosignalrhub")
   .RequireCors("AllowSpecificOrigins");
```

### AzureSignalRSettings

```csharp
public class AzureSignalRSettings
{
    public string? ConnectionString { get; set; }
}
```

### appsettings.json

```json
{
  "AzureSignalRSettings": {
    "ConnectionString": "Endpoint=https://<resource>.service.signalr.net;AccessKey=<key>;Version=1.0;"
  }
}
```

Si `AzureSignalRSettings` es `null` o `ConnectionString` esta vacio o es whitespace, se registra `AddSignalR()` sin Azure SignalR Service. El sistema opera con SignalR local en memoria (fail-safe). Esto es el comportamiento por defecto en desarrollo.

---

## Guia de la API

Cada operacion del servicio expone **tres variantes** con sufijos consistentes:

| Variante | Sufijo | Uso |
|---|---|---|
| DTO del Hub (`TMessage`) | Sin sufijo (p.ej. `BroadcastMessageAsync`) | Ruta principal para envios desde backend |
| Generico (`<T>`) | Sin sufijo, sobrecarga generica (p.ej. `BroadcastMessageAsync<T>`) | Payloads de cualquier tipo desde backend |
| Texto (`string`) | `*Text*` (p.ej. `BroadcastTextAsync`) | Conveniencia para demos, mensajes simples y envios desde frontend |

Los metodos se agrupan en cuatro categorias: **Broadcast**, **Users**, **Groups** y **Group Management**. Los metodos de `BaseHub` (invocables desde frontend) se documentan al final.

---

### Broadcast

#### BroadcastMessageAsync (TMessage)

Emite el DTO tipado del Hub a todos los clientes conectados.

```csharp
Task BroadcastMessageAsync(TMessage message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `message` | DTO tipado del Hub a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

El cliente recibe el mensaje via `SignalRConstants.BroadcastEvent` (`"broadcastMessage"`) con firma `(name, message)`.

```csharp
var notification = new DemoSignalRNotification(
    Id: 42, Message: "Nuevo contenido disponible", Timestamp: DateTime.UtcNow);

await hubService.BroadcastMessageAsync(notification);
```

#### BroadcastMessageAsync\<T\>

Emite un payload de cualquier tipo a todos los clientes conectados.

```csharp
Task BroadcastMessageAsync<T>(T message, string? name = DefaultSenderName) where T : class;
```

| Parametro | Descripcion |
|---|---|
| `message` | Objeto de cualquier tipo a enviar. Se serializa con `System.Text.Json`. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var customPayload = new { Action = "refresh", Entity = "products", Id = 100 };

await hubService.BroadcastMessageAsync(customPayload, name: "CacheInvalidator");
```

#### BroadcastTextAsync

Emite un mensaje de texto plano a todos los clientes conectados.

```csharp
Task BroadcastTextAsync(string message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `message` | Mensaje de texto a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
await hubService.BroadcastTextAsync("Mantenimiento programado a las 22:00 UTC");
```

---

### Users

#### SendUserAsync (TMessage)

Envia el DTO tipado del Hub a un usuario especifico identificado por `IUserIdProvider`.

```csharp
Task SendUserAsync(string userId, TMessage message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `userId` | Identificador del usuario destino. Debe coincidir con el valor devuelto por `IUserIdProvider`. |
| `message` | DTO tipado del Hub a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

El cliente recibe el mensaje via `SignalRConstants.EchoEvent` (`"echo"`) con firma `(name, message)`.

```csharp
var notification = new DemoSignalRNotification(
    Id: 99, Message: "Tu pedido ha sido enviado", Timestamp: DateTime.UtcNow);

await hubService.SendUserAsync("user-42", notification);
```

#### SendUserAsync\<T\>

Envia un payload de cualquier tipo a un usuario especifico.

```csharp
Task SendUserAsync<T>(string userId, T message, string? name = DefaultSenderName) where T : class;
```

| Parametro | Descripcion |
|---|---|
| `userId` | Identificador del usuario destino. |
| `message` | Objeto de cualquier tipo a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var status = new { Online = true, LastSeen = DateTime.UtcNow };

await hubService.SendUserAsync("user-42", status);
```

#### SendUserTextAsync

Envia un mensaje de texto plano a un usuario especifico.

```csharp
Task SendUserTextAsync(string userId, string message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `userId` | Identificador del usuario destino. |
| `message` | Mensaje de texto a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
await hubService.SendUserTextAsync("user-42", "Tienes una nueva notificacion privada");
```

#### SendUsersAsync (TMessage)

Envia el DTO tipado del Hub a una lista de usuarios simultaneamente.

```csharp
Task SendUsersAsync(IReadOnlyList<string> userIds, TMessage message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `userIds` | Lista de identificadores de usuario destino. |
| `message` | DTO tipado del Hub a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var userIds = new[] { "user-10", "user-20", "user-30" };
var notification = new DemoSignalRNotification(null, "Recordatorio grupal", DateTime.UtcNow);

await hubService.SendUsersAsync(userIds, notification);
```

#### SendUsersAsync\<T\>

Envia un payload de cualquier tipo a una lista de usuarios.

```csharp
Task SendUsersAsync<T>(IReadOnlyList<string> userIds, T message, string? name = DefaultSenderName) where T : class;
```

| Parametro | Descripcion |
|---|---|
| `userIds` | Lista de identificadores de usuario destino. |
| `message` | Objeto de cualquier tipo a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var alert = new { Severity = "High", Component = "Auth" };

await hubService.SendUsersAsync(new[] { "admin-1", "admin-2" }, alert, name: "Monitoring");
```

#### SendUsersTextAsync

Envia un mensaje de texto plano a una lista de usuarios.

```csharp
Task SendUsersTextAsync(IReadOnlyList<string> userIds, string message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `userIds` | Lista de identificadores de usuario destino. |
| `message` | Mensaje de texto a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
await hubService.SendUsersTextAsync(
    new[] { "user-10", "user-20" }, "Recordatorio: reunion en 5 minutos");
```

---

### Groups

#### SendGroupAsync (TMessage)

Envia el DTO tipado del Hub a todos los miembros de un grupo.

```csharp
Task SendGroupAsync(string groupName, TMessage message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo destino. |
| `message` | DTO tipado del Hub a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var notification = new DemoSignalRNotification(
    Id: 1, Message: "Actualizacion del grupo DemoGroup", Timestamp: DateTime.UtcNow);

await hubService.SendGroupAsync("DemoGroup", notification);
```

#### SendGroupAsync\<T\>

Envia un payload de cualquier tipo a todos los miembros de un grupo.

```csharp
Task SendGroupAsync<T>(string groupName, T message, string? name = DefaultSenderName) where T : class;
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo destino. |
| `message` | Objeto de cualquier tipo a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var teamUpdate = new { ProjectId = 5, Status = "Completed" };

await hubService.SendGroupAsync("team-alpha", teamUpdate, name: "PM");
```

#### SendGroupTextAsync

Envia un mensaje de texto plano a todos los miembros de un grupo.

```csharp
Task SendGroupTextAsync(string groupName, string message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo destino. |
| `message` | Mensaje de texto a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
await hubService.SendGroupTextAsync("Moderators", "Nuevo reporte pendiente de revision");
```

#### SendGroupsAsync (TMessage)

Envia el DTO tipado del Hub a multiples grupos simultaneamente.

```csharp
Task SendGroupsAsync(IReadOnlyList<string> groupNames, TMessage message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `groupNames` | Lista de nombres de grupo destino. |
| `message` | DTO tipado del Hub a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var groups = new[] { "user-10", "user-20", "Moderators" };
var notification = new DemoSignalRNotification(null, "Aviso multi-grupo", DateTime.UtcNow);

await hubService.SendGroupsAsync(groups, notification);
```

#### SendGroupsAsync\<T\>

Envia un payload de cualquier tipo a multiples grupos.

```csharp
Task SendGroupsAsync<T>(IReadOnlyList<string> groupNames, T message, string? name = DefaultSenderName) where T : class;
```

| Parametro | Descripcion |
|---|---|
| `groupNames` | Lista de nombres de grupo destino. |
| `message` | Objeto de cualquier tipo a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var announcement = new { Title = "Deploy v2.0", ETA = "in 10 min" };

await hubService.SendGroupsAsync(new[] { "dev-team", "qa-team" }, announcement);
```

#### SendGroupsTextAsync

Envia un mensaje de texto plano a multiples grupos.

```csharp
Task SendGroupsTextAsync(IReadOnlyList<string> groupNames, string message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `groupNames` | Lista de nombres de grupo destino. |
| `message` | Mensaje de texto a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
await hubService.SendGroupsTextAsync(
    new[] { "user-10", "user-20" }, "Mantenimiento en 30 minutos");
```

#### SendGroupExceptAsync (TMessage)

Envia el DTO tipado del Hub a un grupo excluyendo ciertas conexiones.

```csharp
Task SendGroupExceptAsync(string groupName, IReadOnlyList<string> connectionIdExcept,
    TMessage message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo destino. |
| `connectionIdExcept` | Lista de connection IDs a excluir del envio. |
| `message` | DTO tipado del Hub a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var notification = new DemoSignalRNotification(42, "Nuevo mensaje en chat", DateTime.UtcNow);

await hubService.SendGroupExceptAsync("Moderators", new[] { callerConnectionId }, notification);
```

#### SendGroupExceptAsync\<T\>

Envia un payload de cualquier tipo a un grupo excluyendo ciertas conexiones.

```csharp
Task SendGroupExceptAsync<T>(string groupName, IReadOnlyList<string> connectionIdExcept,
    T message, string? name = DefaultSenderName) where T : class;
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo destino. |
| `connectionIdExcept` | Lista de connection IDs a excluir. |
| `message` | Objeto de cualquier tipo a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
var poll = new { Question = "Lunch?", Options = new[] { "Yes", "No" } };

await hubService.SendGroupExceptAsync("team", new[] { voterConnId }, poll);
```

#### SendGroupExceptTextAsync

Envia un mensaje de texto plano a un grupo excluyendo ciertas conexiones.

```csharp
Task SendGroupExceptTextAsync(string groupName, IReadOnlyList<string> connectionIdExcept,
    string message, string? name = DefaultSenderName);
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo destino. |
| `connectionIdExcept` | Lista de connection IDs a excluir. |
| `message` | Mensaje de texto a enviar. |
| `name` | Nombre del emisor. Por defecto `"_SYSTEM_"`. |
| **Retorna** | `Task` completada cuando el mensaje se ha enviado. |

```csharp
await hubService.SendGroupExceptTextAsync(
    "Moderators", new[] { callerConnectionId }, "Nuevo ticket asignado");
```

---

### Group Management

#### JoinConnectionToGroupAsync

Agrega una conexion especifica a un grupo nombrado.

```csharp
Task JoinConnectionToGroupAsync(string connectionId, string groupName);
```

| Parametro | Descripcion |
|---|---|
| `connectionId` | ID de la conexion a agregar al grupo. |
| `groupName` | Nombre del grupo destino. |
| **Retorna** | `Task` completada cuando la conexion se ha unido al grupo. |

```csharp
await hubService.JoinConnectionToGroupAsync(Context.ConnectionId, "Moderators");
```

#### LeaveConnectionFromGroupAsync

Elimina una conexion especifica de un grupo nombrado.

```csharp
Task LeaveConnectionFromGroupAsync(string connectionId, string groupName);
```

| Parametro | Descripcion |
|---|---|
| `connectionId` | ID de la conexion a eliminar del grupo. |
| `groupName` | Nombre del grupo origen. |
| **Retorna** | `Task` completada cuando la conexion ha salido del grupo. |

```csharp
await hubService.LeaveConnectionFromGroupAsync(Context.ConnectionId, "Moderators");
```

---

### BaseHub: metodos invocables desde frontend

Estos metodos **no pertenecen a `ISignalRHubService`**. Estan definidos como `virtual` en `BaseHub<THub, TMessage>` y son invocables desde el cliente JavaScript via `connection.invoke("MethodName", ...)`. Heredar de `BaseHub` los proporciona todos sin escribir codigo adicional.

Internamente usan las variantes `*Text*` del servicio (`BroadcastTextAsync`, `SendUserTextAsync`, etc.) con `SignalRConstants.EchoEvent` / `SignalRConstants.BroadcastEvent`. El nombre del emisor se toma de `Context.UserIdentifier`, con fallback a `"anonymous"`.

#### JoinGroup

```csharp
public virtual async Task JoinGroup(string groupName)
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo al que unirse. |

```javascript
await connection.invoke("JoinGroup", "Moderators");
```

#### LeaveGroup

```csharp
public virtual async Task LeaveGroup(string groupName)
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo del que salir. |

```javascript
await connection.invoke("LeaveGroup", "Moderators");
```

#### SendToAll

```csharp
public virtual async Task SendToAll(string message)
```

| Parametro | Descripcion |
|---|---|
| `message` | Mensaje de texto a enviar. |

```javascript
await connection.invoke("SendToAll", "Hola a todos");
```

#### SendToUser

```csharp
public virtual async Task SendToUser(string userId, string message)
```

| Parametro | Descripcion |
|---|---|
| `userId` | Identificador del usuario destino. |
| `message` | Mensaje de texto a enviar. |

```javascript
await connection.invoke("SendToUser", "user-42", "Mensaje privado");
```

#### SendToUsers

```csharp
public virtual async Task SendToUsers(IReadOnlyList<string> userIds, string message)
```

| Parametro | Descripcion |
|---|---|
| `userIds` | Lista de identificadores de usuario destino. |
| `message` | Mensaje de texto a enviar. |

```javascript
await connection.invoke("SendToUsers", ["user-10", "user-20"], "Aviso");
```

#### SendToGroup

```csharp
public virtual async Task SendToGroup(string groupName, string message)
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo destino. |
| `message` | Mensaje de texto a enviar. |

```javascript
await connection.invoke("SendToGroup", "Moderators", "Reunion urgente");
```

#### SendToGroups

```csharp
public virtual async Task SendToGroups(IReadOnlyList<string> groupNames, string message)
```

| Parametro | Descripcion |
|---|---|
| `groupNames` | Lista de nombres de grupo destino. |
| `message` | Mensaje de texto a enviar. |

```javascript
await connection.invoke("SendToGroups", ["Moderators", "Admins"], "Aviso");
```

#### SendToGroupExcept

```csharp
public virtual async Task SendToGroupExcept(string groupName,
    IReadOnlyList<string> excludedConnectionIds, string message)
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo destino. |
| `excludedConnectionIds` | Lista de connection IDs a excluir. |
| `message` | Mensaje de texto a enviar. |

```javascript
await connection.invoke("SendToGroupExcept", "Moderators", ["conn-123"], "Solo los demas");
```

#### SendToCaller

Envia un eco solo a la conexion que lo invoca. Usa `Clients.Caller` directamente (no pasa por `ISignalRHubService`).

```csharp
public virtual async Task SendToCaller(string message)
```

| Parametro | Descripcion |
|---|---|
| `message` | Mensaje de texto a enviar. |

```javascript
await connection.invoke("SendToCaller", "Este mensaje solo lo ves tu");
```

#### SendToOthers

Envia un broadcast a todos menos al que invoca. Usa `Clients.Others` directamente.

```csharp
public virtual async Task SendToOthers(string message)
```

| Parametro | Descripcion |
|---|---|
| `message` | Mensaje de texto a enviar. |

```javascript
await connection.invoke("SendToOthers", "Alguien se ha conectado");
```

#### SendToOthersInGroup

Envia un mensaje a los miembros de un grupo excepto al caller. Usa `Clients.OthersInGroup` directamente.

```csharp
public virtual async Task SendToOthersInGroup(string groupName, string message)
```

| Parametro | Descripcion |
|---|---|
| `groupName` | Nombre del grupo destino. |
| `message` | Mensaje de texto a enviar. |

```javascript
await connection.invoke("SendToOthersInGroup", "Moderators", "Nuevo moderador disponible");
```

### OnConnectedAsync (en BaseHub)

```csharp
public override async Task OnConnectedAsync()
{
    // Auto-join a grupo por usuario (comentado por defecto):
    // var userId = Context.UserIdentifier;
    // var connectionId = Context.ConnectionId;
    // if (!string.IsNullOrWhiteSpace(userId))
    //     await hubService.JoinConnectionToGroupAsync(connectionId, $"user-{userId}");

    await base.OnConnectedAsync();
}
```

El auto-join al grupo `user-{userId}` esta **comentado por defecto**. Si tu aplicacion necesita agrupar conexiones por usuario para envios dirigidos desde backend, descomenta las lineas en tu Hub o sobrescribe `OnConnectedAsync`.

---

## Implementacion interna

### AzureSignalRHubService<THub, TMessage>

```csharp
public class AzureSignalRHubService<THub, TMessage>(IHubContext<THub> hubContext)
    : ISignalRHubService<THub, TMessage>
    where THub : Hub
    where TMessage : class
{
    private const string DefaultSenderName = "_SYSTEM_";
}
```

El constructor recibe unicamente `IHubContext<THub>`, registrado por ASP.NET Core como Singleton. El servicio es stateless: no mantiene cache de conexiones ni estado interno. Todos los metodos delegan a `hubContext.Clients` via `SendAsync` con las constantes de `SignalRConstants`.

### SignalRConstants

```csharp
public static class SignalRConstants
{
    public const string EchoEvent = "echo";
    public const string BroadcastEvent = "broadcastMessage";
}
```

Centraliza los nombres de evento que el cliente JavaScript debe registrar. Tanto `AzureSignalRHubService` como `BaseHub` referencian estas constantes, garantizando que servidor y frontend usen los mismos nombres.

### Metodos client-side invocados

| Categoria de metodo | Client event | Constante |
|---|---|---|
| Broadcast (`BroadcastMessageAsync`, `BroadcastTextAsync`) | `"broadcastMessage"` | `SignalRConstants.BroadcastEvent` |
| Dirigidos (`SendUser*`, `SendGroup*`, `SendUsers*`, `SendGroups*`, `SendGroupExcept*`) | `"echo"` | `SignalRConstants.EchoEvent` |
| `SendToCaller` (BaseHub) | `"echo"` | `SignalRConstants.EchoEvent` |
| `SendToOthers` (BaseHub) | `"broadcastMessage"` | `SignalRConstants.BroadcastEvent` |
| `SendToOthersInGroup` (BaseHub) | `"broadcastMessage"` | `SignalRConstants.BroadcastEvent` |

El cliente debe registrar handlers para estos dos eventos:

```javascript
connection.on(SignalRConstants.BroadcastEvent, (name, message) => { ... });
connection.on(SignalRConstants.EchoEvent, (name, message) => { ... });

// O con strings literales:
connection.on("broadcastMessage", (name, message) => { ... });
connection.on("echo", (name, message) => { ... });
```

### BaseHub<THub, TMessage>

La clase base proporciona `[Authorize]` y todos los metodos de envio invocables desde frontend. `DemoSignalRHub` ahora se reduce a 3 lineas:

```csharp
public class DemoSignalRHub : BaseHub<DemoSignalRHub, DemoSignalRNotification>
{
    public DemoSignalRHub(ISignalRHubService<DemoSignalRHub, DemoSignalRNotification> hubService)
        : base(hubService) { }
}
```

Para crear un Hub nuevo, simplemente hereda de `BaseHub<THub, TMessage>` con tu DTO. Si no necesitas los metodos predefinidos de `BaseHub`, hereda de `Hub` directamente y aplica `[Authorize]` manualmente.

### DemoSignalRHubService (patron wrapper)

```csharp
internal sealed class DemoSignalRHubService(
    ISignalRHubService<DemoSignalRHub, DemoSignalRNotification> hubService)
    : IDemoSignalRHubService
{
    public async Task NotifyUserAsync(string userId, DemoSignalRNotification message) =>
        await hubService.SendUserAsync(userId, message);

    public async Task NotifyGroupAsync(string groupName, DemoSignalRNotification message) =>
        await hubService.SendGroupAsync(groupName, message);

    public async Task BroadcastMessageAsync(DemoSignalRNotification message) =>
        await hubService.BroadcastMessageAsync(message);
}
```

El wrapper usa las variantes `TMessage` (DTO del Hub). Al ser la primera sobrecarga, no necesita explicitamente `<DemoSignalRNotification>` en la llamada; C# resuelve la sobrecarga correcta automaticamente.

Es `internal sealed`: no se expone fuera de `Akay.Be.Infrastructure`. Se registra como **Scoped**.

### SignalRConfiguration (DI)

```csharp
public static class SignalRConfiguration
{
    public static IServiceCollection AddSignalR(
        this IServiceCollection services, AzureSignalRSettings? settings)
    {
        if (string.IsNullOrWhiteSpace(settings?.ConnectionString))
            services.AddSignalR();
        else
            services.AddSignalR().AddAzureSignalR(
                options => options.ConnectionString = settings.ConnectionString);

        services.AddTransient(
            typeof(ISignalRHubService<,>), typeof(AzureSignalRHubService<,>));

        return services;
    }
}
```

La decision de modo es binaria y ocurre en startup. El tipo abierto generico se registra como Transient porque el servicio no tiene estado.

---

## Convenciones de la API en tres capas

Cada dominio (Broadcast, User, Group) ofrece tres variantes de envio con reglas de nombrado predecibles:

| Tipo de payload | Convencion | Ejemplo (Broadcast) |
|---|---|---|
| DTO del Hub (`TMessage`) | `{Action}Async` | `BroadcastMessageAsync(TMessage)` |
| Generico (`<T>`) | `{Action}Async<T>` | `BroadcastMessageAsync<T>(T)` |
| Texto (`string`) | `{Action}TextAsync` | `BroadcastTextAsync(string)` |

### Prioridad de seleccion

Cuando consumes `ISignalRHubService` con tipos cerrados (p.ej. `ISignalRHubService<DemoSignalRHub, DemoSignalRNotification>`), C# resuelve automaticamente la sobrecarga de `TMessage` si el argumento coincide con `DemoSignalRNotification`. Para payloads de otro tipo, usa la sobrecarga generica `<T>` o la variante `*Text*`.

```csharp
// Resuelve SendUserAsync(string, TMessage, string?) -> la variante DTO
await hubService.SendUserAsync("user-42", demoNotification);

// Resuelve SendUserAsync<T>(string, T, string?) -> la variante generica
await hubService.SendUserAsync("user-42", new { Temp = 42 });

// Resuelve SendUserTextAsync(string, string, string?) -> la variante texto
await hubService.SendUserTextAsync("user-42", "hola");
```

---

## Health Check

Este componente **no incluye health check**. SignalR no expone un endpoint de verificacion de conectividad a traves de esta abstraccion.

Para verificar que SignalR esta operativo se recomienda:
- En modo local: el health check de la aplicacion (`/healthz`) ya verifica que el servidor responde, lo que implica que SignalR en memoria esta disponible.
- En modo Azure: monitorizar el estado del recurso Azure SignalR Service desde Azure Portal o Azure Monitor.

---

## Consideraciones

### Autenticacion obligatoria

`BaseHub<THub, TMessage>` aplica `[Authorize]`. Todos los hubs que hereden de el requieren que el cliente este autenticado. Si el cliente no envia un token JWT valido durante la negociacion, SignalR rechaza la conexion.

Para hubs sin autenticacion, no heredes de `BaseHub`; implementa `Hub` directamente.

### Modo local vs Azure

- **Desarrollo (sin connection string):** SignalR opera en memoria dentro del proceso. Solo funciona con una unica instancia del servidor. Si hay multiples instancias (load balancing), los mensajes no se comparten entre ellas.
- **Produccion (con connection string):** Azure SignalR Service actua como proxy y enruta los mensajes a todas las instancias. Requiere que la connection string sea valida y tenga permisos de acceso.

No hay hot-swap: el modo se decide en startup y no cambia durante la ejecucion.

### Auto-join por usuario deshabilitado por defecto

El `OnConnectedAsync` de `BaseHub` tiene comentado el codigo que une la conexion al grupo `user-{userId}`. Si tu aplicacion necesita este comportamiento, descomentalo en tu Hub concreto o sobrescribe `OnConnectedAsync`. Esto evita crear grupos innecesarios en escenarios donde no se requieren envios dirigidos.

### Nombres de client events

Los nombres de evento estan definidos en `SignalRConstants`: `"broadcastMessage"` y `"echo"`. El cliente JavaScript debe registrar handlers con esos nombres exactos:

```javascript
connection.on("broadcastMessage", (name, message) => {
    console.log(`[GLOBAL] ${name}:`, message);
});

connection.on("echo", (name, message) => {
    console.log(`[DIRECTO] ${name}:`, message);
});
```

Si el frontend usa nombres distintos, los mensajes se pierden silenciosamente (no hay error en el servidor).

### Variantes de envio y serializacion

- Las variantes `TMessage` y `<T>` envian objetos que SignalR serializa con `System.Text.Json` usando la configuracion por defecto de ASP.NET Core (camelCase). El cliente recibe un objeto JSON.
- Las variantes `*Text*` envian strings planos.
- Todas las variantes de una misma categoria llegan al mismo handler del cliente (`echo` o `broadcastMessage`), con la misma firma `(name, message)`.

### Membresia a grupos por connectionId

SignalR gestiona grupos por `connectionId`, no por `userId`. Un mismo usuario puede tener multiples conexiones (pestanas, dispositivos). Unirse a un grupo afecta solo a esa conexion especifica. Al desconectarse, SignalR elimina automaticamente la conexion de todos sus grupos.

### Opciones al crear un Hub

`BaseHub` ofrece dos caminos:
- **Opcion A:** Heredar de `Hub` directamente y anadir `[Authorize]` y metodos manualmente. Mas control, mas codigo.
- **Opcion B:** Heredar de `BaseHub<THub, TMessage>`. Bateria incluida: autorizacion, 11 metodos de envio y `OnConnectedAsync` con auto-join comentado.

### Sender name

| Origen de la llamada | Valor de `name` |
|---|---|
| Backend (via `ISignalRHubService`) | `"_SYSTEM_"` (por defecto, sobrescribible) |
| Frontend (via metodos de `BaseHub`) | `Context.UserIdentifier` del cliente que invoca, con fallback a `"anonymous"` |

`DefaultSenderName` es `protected const` en la interfaz, accesible desde implementaciones que necesiten el valor por defecto.

---

## Testing

### Tests de integracion

No hay tests de integracion especificos para este componente. Para probar SignalR en modo local no se requiere infraestructura externa (Docker, emuladores). El servidor SignalR en memoria es suficiente.

Para probar el modo Azure se requiere un recurso Azure SignalR Service configurado.

### Mock de ISignalRHubService<THub, TMessage>

Para tests unitarios de consumidores del servicio generico:

```csharp
var mockHub = new Mock<ISignalRHubService<DemoSignalRHub, DemoSignalRNotification>>();

// Broadcast
mockHub.Setup(h => h.BroadcastMessageAsync(
        It.IsAny<DemoSignalRNotification>(), It.IsAny<string?>()))
    .Returns(Task.CompletedTask);

mockHub.Setup(h => h.BroadcastMessageAsync(
        It.IsAny<object>(), It.IsAny<string?>()))
    .Returns(Task.CompletedTask);

mockHub.Setup(h => h.BroadcastTextAsync(
        It.IsAny<string>(), It.IsAny<string?>()))
    .Returns(Task.CompletedTask);

// User
mockHub.Setup(h => h.SendUserAsync(
        "user-42", It.IsAny<DemoSignalRNotification>(), It.IsAny<string?>()))
    .Returns(Task.CompletedTask);

mockHub.Setup(h => h.SendUserTextAsync(
        "user-42", It.IsAny<string>(), It.IsAny<string?>()))
    .Returns(Task.CompletedTask);

// Group
mockHub.Setup(h => h.SendGroupAsync(
        "DemoGroup", It.IsAny<DemoSignalRNotification>(), It.IsAny<string?>()))
    .Returns(Task.CompletedTask);

mockHub.Setup(h => h.SendGroupTextAsync(
        "DemoGroup", It.IsAny<string>(), It.IsAny<string?>()))
    .Returns(Task.CompletedTask);

// Group Management
mockHub.Setup(h => h.JoinConnectionToGroupAsync(
        It.IsAny<string>(), It.IsAny<string>()))
    .Returns(Task.CompletedTask);

mockHub.Setup(h => h.LeaveConnectionFromGroupAsync(
        It.IsAny<string>(), It.IsAny<string>()))
    .Returns(Task.CompletedTask);
```

### Mock de IDemoSignalRHubService

Para tests unitarios de consumidores del wrapper tipado:

```csharp
var mockService = new Mock<IDemoSignalRHubService>();

mockService.Setup(s => s.NotifyUserAsync(
        "user-42", It.IsAny<DemoSignalRNotification>()))
    .Returns(Task.CompletedTask);

mockService.Setup(s => s.NotifyGroupAsync(
        "DemoGroup", It.IsAny<DemoSignalRNotification>()))
    .Returns(Task.CompletedTask);

mockService.Setup(s => s.BroadcastMessageAsync(
        It.IsAny<DemoSignalRNotification>()))
    .Returns(Task.CompletedTask);
```
