
# Messaging

## Que es

`IMessageBus` es una abstraccion sobre Rebus que proporciona envio de comandos point-to-point y publicacion de eventos publish/subscribe, con auto-subscribe automatico para eventos, routing declarativo y retry configurable. La capa de aplicacion no referencia Rebus directamente.

Los consumers implementan `IMessageHandler<T>` y son detectados automaticamente por escaneo de assemblies al registrarse en DI. El adaptador interno `RebusMessageHandlerAdapter` traduce entre `IMessageHandler<T>` (contrato propio) y `IHandleMessages<T>` (contrato Rebus).

**Paquete:** `Akay.To.Core`
**Interfaz principal:** `Akay.To.Core.Application.Abstractions.Messaging.IMessageBus`
**Interfaz de handler:** `Akay.To.Core.Application.Abstractions.Messaging.IMessageHandler<TMessage>`
**Implementacion:** `Akay.To.Core.Infrastructure.DependencyInjection.Rebus.RebusMessageBus` (internal)
**Registro DI:** `Akay.To.Core.Infrastructure.DependencyInjection.RebusMessagingConfiguration.AddRebusMessaging`

Rutas Fully Qualified:
- `Akay.To.Core.Application.Abstractions.Messaging.IMessageBus`
- `Akay.To.Core.Application.Abstractions.Messaging.IMessageHandler<TMessage>`
- `Akay.To.Core.Application.Abstractions.Messaging.IMessage`
- `Akay.To.Core.Application.Abstractions.Messaging.ICommandMessage`
- `Akay.To.Core.Application.Abstractions.Messaging.IIntegrationEvent`
- `Akay.To.Core.Application.Abstractions.Messaging.MessageHandlingException`
- `Akay.To.Core.Infrastructure.Messaging.BaseConsumerToDispatcher`
- `Akay.To.Core.Application.ApplicationSettings.MessagingTransportNames`

---

## Por que usarlo

- **Desacoplamiento total de Rebus:** la capa de aplicacion nunca importa `Rebus.Bus.*`. Si se cambia de broker, solo se modifica infraestructura.
- **Auto-subscribe de eventos:** al arrancar, `RebusSubscriptionHostedService` detecta todos los `IIntegrationEvent` manejados por `IMessageHandler<T>` y llama a `bus.Subscribe<T>()` automaticamente. No hay que subscribirse manualmente.
- **Comandos locales sin configuracion:** si un `ICommandMessage` no tiene route configurada, `SendAsync` usa `SendLocal` y el consumer de la misma aplicacion lo recibe. Ideal para escenarios where van en la misma API.
- **Comandos remotos con routing declarativo:** si un `ICommandMessage` tiene route en `MessagingSettings.Routes`, se envia a la cola del worker. La misma llamada `SendAsync` decide el destino automaticamente.
- **Escaneo automatico de handlers:** `RegisterHandlers` escanea los assemblies pasados, encuentra todos los `IMessageHandler<T>` concretos, los registra en DI y crea los adaptadores Rebus. No hay registro manual de handlers.
- **BaseConsumerToDispatcher para consumers basados en dispatcher:** clase base que recibe un `IDispatcher` y ofrece `ConsumeAsCommand` para traducir mensajes entrantes a comandos del dominio. Maneja reintentos para errores transitorios.
- **Retry configurable:** `MaxDeliveryAttempts` en settings, con cola de errores automatica. Errores funcionales (Validation, NotFound) se loguean y se consumen; errores transitorios (Failure, Timeout, Unavailable, Internal) lanzan `MessageHandlingException` para que Rebus reintente.
- **Transports intercambiables:** InMemory (desarrollo/tests), RabbitMq, Azure Service Bus. Se cambia con un setting, sin tocar codigo.
- **Fail-safe:** si `MessagingSettings` es `null`, no se registra nada. La aplicacion arranca sin mensajeria.
- **Envio diferido (Delay):** retrasar la entrega de un comando con `MessageSendOptions.Delay`. Internamente usa `DeferLocal`/`Defer` de Rebus.
- **TTL por mensaje (TimeToLive):** expiracion configurable por comando o evento. El broker descarta el mensaje si supera el tiempo.
- **Headers funcionales:** adjuntar metadatos de trazabilidad, auditoria, tenant, usuario, idempotencia, etc. a cualquier mensaje sin modificar su payload.

---

## Arquitectura

### IMessageBus

```csharp
public interface IMessageBus
{
    Task SendAsync<TCommand>(TCommand message,
                             CancellationToken cancellationToken = default)
        where TCommand : class, ICommandMessage;

    Task SendAsync<TCommand>(TCommand message,
                             MessageSendOptions options,
                             CancellationToken cancellationToken = default)
        where TCommand : class, ICommandMessage;

    Task PublishAsync<TEvent>(TEvent message,
                              CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;

    Task PublishAsync<TEvent>(TEvent message,
                              MessagePublishOptions options,
                              CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
```

### IMessageHandler\<TMessage\>

```csharp
public interface IMessageHandler<in TMessage>
    where TMessage : class
{
    Task HandleAsync(TMessage message,
                     CancellationToken cancellationToken = default);
}
```

### Tipos de mensaje

```csharp
// Marker base
public interface IMessage;

// Evento (pub/sub) - se auto-suscribe al arrancar
public interface IIntegrationEvent : IMessage;

// Comando (point-to-point) - se envia local o remoto segun la route configurada
public interface ICommandMessage : IMessage;
```

### BaseConsumerToDispatcher

```csharp
public abstract class BaseConsumerToDispatcher(ILogger logger, IDispatcher dispatcher)
{
    protected Task ConsumeAsCommand<TCommand>(TCommand command,
                                             CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<Unit>;

    protected Task ConsumeAsCommand<TMessage, TCommand>(TMessage message,
                                                       Func<TMessage, TCommand> commandFactory,
                                                       CancellationToken cancellationToken = default)
        where TMessage : class
        where TCommand : class, ICommand<Unit>;
}
```

### MessageHandlingException

```csharp
public sealed class MessageHandlingException : Exception
{
    public MessageHandlingException(string message);
    public MessageHandlingException(string message, Exception innerException);
}
```

### MessagingTransportNames

```csharp
public static class MessagingTransportNames
{
    public const string InMemory = "InMemory";
    public const string RabbitMq = "RabbitMq";
    public const string AzureServiceBus = "AzureServiceBus";
}
```

Ubicacion: `Akay.To.Core.Application.ApplicationSettings.MessagingTransportNames`.

### MessagingSettings

```csharp
public sealed class MessagingSettings
{
    public string Transport { get; set; } = MessagingTransportNames.InMemory;
    public string? ConnectionString { get; set; }
    public string InputQueueName { get; set; } = default!;
    public bool AutoRegisterHandlers { get; set; } = true;
    public int MaxDeliveryAttempts { get; set; } = 3;
    public string? ErrorQueueAddress { get; set; }
    public Dictionary<string, string> Routes { get; set; } = new(StringComparer.Ordinal);
}
```

### MessageSendOptions

Opciones para `SendAsync`. Se pueden construir con factories encadenables.

```csharp
public sealed record MessageSendOptions
{
    public static readonly MessageSendOptions Default = new();

    public TimeSpan? Delay { get; init; }
    public TimeSpan? TimeToLive { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; }

    public MessageSendOptions WithDelay(TimeSpan delay);
    public MessageSendOptions WithTimeToLive(TimeSpan timeToLive);
    public MessageSendOptions WithHeader(string key, string value);
}
```

| Propiedad | Descripcion |
|---|---|
| `Delay` | Retrasa la entrega del mensaje. Internamente usa `bus.DeferLocal` o `bus.Defer` segun la route. |
| `TimeToLive` | TTL: el broker puede descartar el mensaje si no se entrega/consume dentro de este tiempo. Se traduce al header `rbs2-time-to-be-received` de Rebus. |
| `Headers` | Headers funcionales propios (ver seccion Headers funcionales). |

### MessagePublishOptions

Opciones para `PublishAsync`.

```csharp
public sealed record MessagePublishOptions
{
    public static readonly MessagePublishOptions Default = new();

    public TimeSpan? TimeToLive { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; }

    public MessagePublishOptions WithTimeToLive(TimeSpan timeToLive);
    public MessagePublishOptions WithHeader(string key, string value);
}
```

| Propiedad | Descripcion |
|---|---|
| `TimeToLive` | TTL para el evento (ver send). |
| `Headers` | Headers funcionales propios. |

---

## Configuracion

### Registro en DI

```csharp
using Akay.To.Core.Infrastructure.DependencyInjection;

// Sin handlers explicitos (usa EntryAssembly como fallback)
services.AddRebusMessaging(settings?.MessagingSettings);

// Con assemblies de handlers explicitos
services.AddRebusMessaging(settings?.MessagingSettings,
    typeof(UserRegisteredConsumer).Assembly);

// En Akay.Be, tipicamente desde InfrastructureRegisterModule:
services.AddRebusMessaging(settings?.MessagingSettings, Assembly.GetEntryAssembly()!);
```

### Que registra

| Servicio | Lifetime |
|---|---|
| `IOptions<MessagingSettings>` | Singleton |
| `InMemNetwork` | Singleton |
| `IMessageBus` → `RebusMessageBus` | Singleton |
| `IBus` (via `AddRebus`) | Singleton |
| Cada `IMessageHandler<T>` encontrado | Transient |
| `IHandleMessages<T>` → `RebusMessageHandlerAdapter<T>` | Transient |
| `RebusSubscriptionRegistry` | Singleton |
| `RebusSubscriptionHostedService` (IHostedService) | Singleton |
| `RebusMessageHealthCheck` (health check `rebus_messaging`) | Transient |

Si `MessagingSettings` es `null`, no se registra nada y el sistema opera sin mensajeria (fail-safe).

### appsettings.json

**API (InMemory, desarrollo):**

```json
{
  "MessagingSettings": {
    "Transport": "InMemory",
    "InputQueueName": "akaybe.api",
    "MaxDeliveryAttempts": 3
  }
}
```

**API con worker externo (RabbitMq):**

```json
{
  "MessagingSettings": {
    "Transport": "RabbitMq",
    "InputQueueName": "akaybe.api",
    "ConnectionString": "amqp://guest:guest@localhost",
    "MaxDeliveryAttempts": 3,
    "Routes": {
      "Akay.Be.Application.Features.Messaging.GenerateLearningHubReportMessage, Akay.Be.Application": "akaybe.worker"
    }
  }
}
```

**Worker (RabbitMq):**

```json
{
  "MessagingSettings": {
    "Transport": "RabbitMq",
    "InputQueueName": "akaybe.worker",
    "ConnectionString": "amqp://guest:guest@localhost",
    "MaxDeliveryAttempts": 3
  }
}
```

### Validacion de settings

Cuando `MessagingSettings` no es null, se valida:
- `Transport` obligatorio y debe ser `InMemory`, `RabbitMq` o `AzureServiceBus`.
- `InputQueueName` obligatorio.
- `ConnectionString` obligatorio si `Transport` es `RabbitMq` o `AzureServiceBus`.
- `MaxDeliveryAttempts` debe ser mayor que 0.
- Si hay `Routes`, ni la clave ni el valor pueden estar vacios.

Si `MessagingSettings` es `null`, no se ejecuta ninguna validacion de mensajeria.

---

## Guia de la API

### Eventos (pub/sub)

#### PublishAsync

Publica un evento a todos los endpoints suscritos. El auto-subscribe registra la suscripcion al arrancar si existe un `IMessageHandler<T>` donde `T : IIntegrationEvent`.

```csharp
Task PublishAsync<TEvent>(TEvent message,
                          CancellationToken cancellationToken = default)
    where TEvent : class, IIntegrationEvent;

Task PublishAsync<TEvent>(TEvent message,
                          MessagePublishOptions options,
                          CancellationToken cancellationToken = default)
    where TEvent : class, IIntegrationEvent;
```

| Parametro | Descripcion |
|---|---|
| `message` | Evento a publicar. Debe implementar `IIntegrationEvent`. Compile-time enforced. Lanza `ArgumentNullException` si es null. |
| `options` | Opciones de envio: `TimeToLive` y `Headers`. Ver `MessagePublishOptions`. |
| `cancellationToken` | Token de cancelacion. Lanza `OperationCanceledException` si se cancela. |
| **Retorna** | `Task` que completa cuando Rebus acepta el mensaje. |
| **Excepciones** | `ArgumentNullException` si `message` o `options` es null; `OperationCanceledException` si se cancela. |

El consumer debe implementar `IMessageHandler<T>` (ver seccion Consumers). Al arrancar, el sistema se suscribe automaticamente a los tipos de evento detectados en los assemblies de handlers.

**Ejemplo: publicar un evento desde un handler de aplicacion**

```csharp
// Definir el evento
public sealed record LearningHubCreatedEvent(int Id,
                                              string Name,
                                              string Description) : IIntegrationEvent;

// Publicar desde un command handler
public async ValueTask<Result<LearningHubResponse>> Handle(
    CreateLearningHubCommand request, CancellationToken cancellationToken)
{
    // ... logica de negocio ...

    await messageBus.PublishAsync(
        new LearningHubCreatedEvent(created.Id, created.Name, created.Description),
        cancellationToken);

    return response;
}
```

**Ejemplo: evento con TTL y headers de trazabilidad**

```csharp
await messageBus.PublishAsync(
    new LearningHubCreatedEvent(created.Id, created.Name, created.Description),
    new MessagePublishOptions
    {
        TimeToLive = TimeSpan.FromHours(1),
        Headers = new Dictionary<string, string>
        {
            ["x-correlation-id"] = Activity.Current?.Id ?? "",
            ["x-user-id"] = currentUser.Id,
        }
    },
    cancellationToken);
```

O con factory methods encadenables:

```csharp
await messageBus.PublishAsync(
    new LearningHubCreatedEvent(created.Id, created.Name, created.Description),
    MessagePublishOptions.Default
        .WithTimeToLive(TimeSpan.FromHours(1))
        .WithHeader("x-correlation-id", correlationId)
        .WithHeader("x-user-id", currentUser.Id),
    cancellationToken);
```

**Ejemplo: publicar desde un controller**

```csharp
[ApiController]
[Route("api/learning-hubs")]
public sealed class LearningHubController(
    IDispatcher dispatcher,
    IMessageBus messageBus) : ControllerBase
{
    [HttpPost]
    public async Task<IResult> Create([FromForm] CreateRequest request,
                                      CancellationToken cancellationToken)
    {
        var command = new CreateLearningHubCommand(...);
        var result = await dispatcher.Send(command, cancellationToken);

        // El evento se publica dentro del handler (CreateLearningHubCommandHandler)
        // No se publica desde el controller para mantener la coherencia transaccional.

        return result.ToCreated(value => $"api/learning-hubs/{value.Id}");
    }
}
```

---

### Comandos (point-to-point)

#### SendAsync

Envia un comando. Solo acepta tipos que implementen `ICommandMessage` (compile-time enforced). El destino se decide automaticamente:

- Si **no tiene route** en `MessagingSettings.Routes`, usa `SendLocal` (misma aplicacion).
- Si **tiene route**, usa `Send` (cola del worker externo).

```csharp
Task SendAsync<TCommand>(TCommand message,
                         CancellationToken cancellationToken = default)
    where TCommand : class, ICommandMessage;

Task SendAsync<TCommand>(TCommand message,
                         MessageSendOptions options,
                         CancellationToken cancellationToken = default)
    where TCommand : class, ICommandMessage;
```

| Parametro | Descripcion |
|---|---|
| `message` | Comando a enviar. Debe implementar `ICommandMessage`. Compile-time enforced. Lanza `ArgumentNullException` si es null. |
| `options` | Opciones de envio: `Delay`, `TimeToLive` y `Headers`. Ver `MessageSendOptions`. |
| `cancellationToken` | Token de cancelacion. Lanza `OperationCanceledException` si se cancela. |
| **Retorna** | `Task` que completa cuando Rebus acepta el mensaje. |
| **Excepciones** | `ArgumentNullException` si `message` o `options` es null; `ArgumentOutOfRangeException` si `Delay <= 0` o `TimeToLive <= 0`; `ArgumentException` si `TimeToLive <= Delay`; `OperationCanceledException`. |

**Ejemplo: comando consumido por la misma API (sin route)**

```csharp
// Definir el mensaje
public sealed record RefreshLearningHubCacheMessage(int Id) : ICommandMessage;

// Consumer en la misma API
public sealed class RefreshCacheConsumer(ILogger<RefreshCacheConsumer> logger)
    : IMessageHandler<RefreshLearningHubCacheMessage>
{
    public Task HandleAsync(RefreshLearningHubCacheMessage message,
                            CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Refreshing cache for hub {Id}", message.Id);
        // Logica de refresco de cache...
        return Task.CompletedTask;
    }
}

// Enviar desde un endpoint
[HttpPost("{id:int}/refresh-cache")]
public async Task<IResult> RefreshCache(int id, CancellationToken cancellationToken)
{
    await messageBus.SendAsync(
        new RefreshLearningHubCacheMessage(id), cancellationToken);

    return TypedResults.Accepted($"api/learning-hubs/{id}/refresh-cache");
}
```

Configuracion: sin entry en `MessagingSettings.Routes`. El mensaje se entrega localmente.

**Ejemplo: comando consumido por un worker (con route)**

API:

```csharp
// Definir el mensaje
public sealed record GenerateLearningHubReportMessage(int Id) : ICommandMessage;

// Enviar desde un endpoint
[HttpPost("{id:int}/generate-report")]
public async Task<IResult> GenerateReport(int id, CancellationToken cancellationToken)
{
    await messageBus.SendAsync(
        new GenerateLearningHubReportMessage(id), cancellationToken);

    return TypedResults.Accepted($"api/learning-hubs/{id}/generate-report");
}
```

appsettings.json de la API:

```json
{
  "MessagingSettings": {
    "Transport": "RabbitMq",
    "InputQueueName": "akaybe.api",
    "ConnectionString": "amqp://guest:guest@localhost",
    "Routes": {
      "Akay.Be.Application.Features.Messaging.GenerateLearningHubReportMessage, Akay.Be.Application": "akaybe.worker"
    }
  }
}
```

Worker:

```csharp
// Consumer en el worker (mismo tipo de mensaje)
public sealed class GenerateLearningHubReportConsumer(
    ILogger<GenerateLearningHubReportConsumer> logger)
    : IMessageHandler<GenerateLearningHubReportMessage>
{
    public Task HandleAsync(GenerateLearningHubReportMessage message,
                            CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Received GenerateLearningHubReportMessage for LearningHubId={Id}",
            message.Id);
        // Generar informe...
        return Task.CompletedTask;
    }
}
```

appsettings.json del worker:

```json
{
  "MessagingSettings": {
    "Transport": "RabbitMq",
    "InputQueueName": "akaybe.worker",
    "ConnectionString": "amqp://guest:guest@localhost"
  }
}
```

La clave de route en `Routes` puede ser:
- `AssemblyQualifiedName`: `"Namespace.Type, Assembly, Version=..., Culture=..., PublicKeyToken=..."`
- `FullName`: `"Namespace.Type"` (solo si el tipo es resolvable desde el assembly en ejecucion)

---

### Envio diferido (Delay)

Retrasa la entrega del comando usando `DeferLocal` o `Defer` de Rebus segun route. El mensaje se almacena en el timeout manager de Rebus hasta que vence el delay.

```csharp
await messageBus.SendAsync(
    new GenerateLearningHubReportMessage(42),
    MessageSendOptions.Default.WithDelay(TimeSpan.FromMinutes(10)),
    cancellationToken);
```

**Ejemplo: notificacion programada**

```csharp
[HttpPost("{id:int}/schedule-report")]
public async Task<IResult> ScheduleReport(int id, CancellationToken ct)
{
    await messageBus.SendAsync(
        new GenerateLearningHubReportMessage(id),
        MessageSendOptions.Default
            .WithDelay(TimeSpan.FromHours(2))
            .WithHeader("x-user-id", currentUser.Id),
        ct);

    return TypedResults.Accepted($"api/learning-hubs/{id}/schedule-report");
}
```

### TTL (TimeToLive)

El broker puede descartar el mensaje si no se entrega o procesa dentro del tiempo especificado. Util cuando el mensaje pierde validez con el tiempo.

```csharp
await messageBus.SendAsync(
    new RefreshLearningHubCacheMessage(42),
    MessageSendOptions.Default.WithTimeToLive(TimeSpan.FromMinutes(5)),
    cancellationToken);
```

**Ejemplo: evento con TTL corto**

```csharp
await messageBus.PublishAsync(
    new LearningHubCreatedEvent(created.Id, created.Name, created.Description),
    MessagePublishOptions.Default.WithTimeToLive(TimeSpan.FromHours(1)),
    cancellationToken);
```

### Delay + TTL juntos

Si se especifican ambos, `TimeToLive` debe ser mayor que `Delay`. Se valida en tiempo de ejecucion.

```csharp
await messageBus.SendAsync(
    new GenerateLearningHubReportMessage(42),
    MessageSendOptions.Default
        .WithDelay(TimeSpan.FromMinutes(10))
        .WithTimeToLive(TimeSpan.FromHours(1)),
    cancellationToken);
```

---

## Headers funcionales

Los headers no forman parte del payload del mensaje. Son metadatos que se transportan junto al mensaje en el bus, utiles para trazabilidad, auditoria, idempotencia y contexto de negocio.

Ejemplos de uso:

| Header | Propósito |
|---|---|
| `x-tenant-id` | Identificar el tenant/cliente que origino el mensaje. |
| `x-user-id` | Identificar el usuario que inicio la accion. |
| `x-correlation-id` | Agrupar logs y trazas de una misma operación. |
| `x-causation-id` | Indicar que comando/evento causó este mensaje. |
| `x-idempotency-key` | Evitar procesamiento duplicado del mismo mensaje. |
| `x-message-schema-version` | Versionar el esquema del payload. |
| `x-locale` | Indicar cultura/idioma para procesamiento. |
| `x-priority` | Decidir prioridad logica en el handler. |
| `x-request-source` | Identificar el origen (API, worker, job, admin). |
| `traceparent` | Propagar contexto de trazas W3C / OpenTelemetry. |
| `tracestate` | Propagar estado adicional de trazas. |

### Incluir headers en un comando

```csharp
await messageBus.SendAsync(
    new GenerateLearningHubReportMessage(id),
    MessageSendOptions.Default
        .WithHeader("x-correlation-id", correlationId)
        .WithHeader("x-user-id", currentUser.Id)
        .WithHeader("x-tenant-id", currentTenant.Id),
    cancellationToken);
```

### Incluir headers en un evento

```csharp
await messageBus.PublishAsync(
    new LearningHubCreatedEvent(created.Id, created.Name, created.Description),
    MessagePublishOptions.Default
        .WithHeader("traceparent", Activity.Current?.Id ?? "")
        .WithHeader("x-correlation-id", correlationId),
    cancellationToken);
```

### Acceder a los headers desde un consumer

Los headers los recibe el consumer a traves del contexto de Rebus (no expuesto en `IMessageHandler<T>` directamente). Si necesitas acceder a ellos, puedes inyectar `IMessageContext` de Rebus en el handler:

```csharp
public sealed class MyConsumer(
    ILogger<MyConsumer> logger,
    IMessageContext messageContext)
    : IMessageHandler<MyMessage>
{
    public Task HandleAsync(MyMessage message,
                            CancellationToken cancellationToken = default)
    {
        var headers = messageContext.Headers;

        logger.LogInformation("Tenant: {Tenant}, User: {User}",
            headers.GetValue("x-tenant-id"),
            headers.GetValue("x-user-id"));

        return Task.CompletedTask;
    }
}
```

### Headers reservados

No se pueden sobreescribir headers internos de Rebus. La validacion bloquea estos nombres (constantes de `Rebus.Messages.Headers`):

| Header Rebus | Uso interno |
|---|---|
| `rbs2-msg-id` | ID del mensaje |
| `rbs2-type` | Tipo .NET del mensaje |
| `rbs2-correlation-id` | Correlacion automatica |
| `rbs2-return-address` | Direccion de reply |
| `rbs2-sender-address` | Direccion del emisor |
| `rbs2-deferred-until` | Entrega diferida |
| `rbs2-deferred-recipient` | Destinatario tras delay |
| `rbs2-defer-count` | Numero de deferrals |
| `rbs2-time-to-be-received` | TTL del mensaje |
| `rbs2-intent` | Send vs Publish |
| `rbs2-sent-time` | Fecha de envio |
| `rbs2-error-details` | Detalles de error |
| `rbs2-source-queue` | Cola origen en error queue |
| `rbs2-delivery-count` | Intentos de entrega |

Si intentas usar uno de estos como clave en `Headers` de `MessageSendOptions` / `MessagePublishOptions`, se lanza `ArgumentException`.

---

## Consumers

Los consumers implementan `IMessageHandler<T>` y son detectados automaticamente por escaneo de assemblies. Pueden heredar de `BaseConsumerToDispatcher` para traducir mensajes a comandos via `IDispatcher`.

#### Consumer basico (sin dispatcher)

```csharp
public sealed class GenerateLearningHubReportConsumer(
    ILogger<GenerateLearningHubReportConsumer> logger)
    : IMessageHandler<GenerateLearningHubReportMessage>
{
    public Task HandleAsync(GenerateLearningHubReportMessage message,
                            CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Processing report for hub {Id}", message.Id);
        return Task.CompletedTask;
    }
}
```

#### Consumer con dispatcher (BaseConsumerToDispatcher)

```csharp
public sealed class UserRegisteredConsumer(
    ILogger<UserRegisteredConsumer> logger,
    IDispatcher dispatcher)
    : BaseConsumerToDispatcher(logger, dispatcher),
      IMessageHandler<LearningHubCreatedEvent>
{
    public Task HandleAsync(LearningHubCreatedEvent message,
                            CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Handling LearningHubCreatedEvent: Id={Id}, Name={Name}",
            message.Id, message.Name);

        return ConsumeAsCommand(
            message,
            static ev => new SendNewLearningHubNotification(
                ev.Id, ev.Name, ev.Description),
            cancellationToken);
    }
}
```

`ConsumeAsCommand` delega al `IDispatcher`, que ejecuta el pipeline completo (validacion, retry, compensacion). Si el resultado es fallido y el error es transitorio, lanza `MessageHandlingException` para que Rebus reintente.

---

### BaseConsumerToDispatcher

Metodos protegidos de `BaseConsumerToDispatcher` para consumers que heredan de el. No forman parte de la interfaz `IMessageHandler<T>`.

Namespace: `Akay.To.Core.Infrastructure.Messaging.BaseConsumerToDispatcher`

#### ConsumeAsCommand\<TCommand\>

Envia un comando directamente al dispatcher.

```csharp
protected Task ConsumeAsCommand<TCommand>(TCommand command,
                                          CancellationToken cancellationToken = default)
    where TCommand : class, ICommand<Unit>;
```

| Parametro | Descripcion |
|---|---|
| `command` | Comando a ejecutar via `IDispatcher.Send`. Lanza `ArgumentNullException` si es null. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Task` que completa si el resultado es exito. Lanza `MessageHandlingException` si el error es transitorio. |

#### ConsumeAsCommand\<TMessage, TCommand\>

Transforma un mensaje entrante en un comando y lo envia al dispatcher.

```csharp
protected Task ConsumeAsCommand<TMessage, TCommand>(
    TMessage message,
    Func<TMessage, TCommand> commandFactory,
    CancellationToken cancellationToken = default)
    where TMessage : class
    where TCommand : class, ICommand<Unit>;
```

| Parametro | Descripcion |
|---|---|
| `message` | Mensaje entrante. Lanza `ArgumentNullException` si es null. |
| `commandFactory` | Funcion que transforma el mensaje en un comando. Lanza `ArgumentNullException` si es null o retorna null. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Task` que completa si el resultado es exito. |

**Ejemplo con factory:**

```csharp
return ConsumeAsCommand(
    message,
    static ev => new SendNewLearningHubNotification(ev.Id, ev.Name, ev.Description),
    cancellationToken);
```

---

### Registro manual de handlers

#### AddRebusHandler\<TMessage, THandler\>

Registra un handler manualmente para un tipo de mensaje especifico. Util cuando `AutoRegisterHandlers = false` o cuando necesitas control explicito sobre que handlers se registran.

```csharp
public static IServiceCollection AddRebusHandler<TMessage, THandler>(
    this IServiceCollection services)
    where TMessage : class
    where THandler : class, IMessageHandler<TMessage>;
```

| Parametro | Descripcion |
|---|---|
| `TMessage` | Tipo del mensaje (`ICommandMessage` o `IIntegrationEvent`). |
| `THandler` | Implementacion concreta de `IMessageHandler<TMessage>`. |
| **Retorna** | `IServiceCollection` para encadenamiento. |

**Ejemplo:**

```csharp
services.AddRebusHandler<LearningHubCreatedEvent, UserRegisteredConsumer>();
services.AddRebusHandler<GenerateLearningHubReportMessage, GenerateLearningHubReportConsumer>();
```

Esto registra:
- `THandler` como transient (para inyeccion directa si es necesario).
- `IMessageHandler<TMessage>` → `THandler` como transient.
- `IHandleMessages<TMessage>` → `RebusMessageHandlerAdapter<TMessage, THandler>` como transient (adaptador Rebus).

No requiere pasar assemblies de escaneo. No afecta el auto-subscribe de eventos (debes suscribirte manualmente si no usas auto-registro).

---

## Implementacion interna

### RebusMessageBus

```csharp
internal sealed class RebusMessageBus(IBus bus, IOptions<MessagingSettings> options) : IMessageBus
{
    public Task SendAsync<TCommand>(TCommand message, CancellationToken ct = default)
        where TCommand : class, ICommandMessage
        => SendAsync(message, MessageSendOptions.Default, ct);

    public async Task SendAsync<TCommand>(TCommand message,
        MessageSendOptions options, CancellationToken ct = default)
        where TCommand : class, ICommandMessage
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();

        ValidateOptions(options);
        var headers = BuildHeaders(options);

        if (options.Delay is not null)
        {
            if (!HasRoute(message.GetType()))
                await bus.DeferLocal(options.Delay.Value, message, headers).ConfigureAwait(false);
            else
                await bus.Defer(options.Delay.Value, message, headers).ConfigureAwait(false);
            return;
        }

        if (!HasRoute(message.GetType()))
            await bus.SendLocal(message, headers).ConfigureAwait(false);
        else
            await bus.Send(message, headers).ConfigureAwait(false);
    }

    public Task PublishAsync<TEvent>(TEvent message, CancellationToken ct = default)
        where TEvent : class, IIntegrationEvent
        => PublishAsync(message, MessagePublishOptions.Default, ct);

    public async Task PublishAsync<TEvent>(TEvent message,
        MessagePublishOptions options, CancellationToken ct = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();

        ValidateOptions(options);
        var headers = BuildHeaders(options);

        await bus.Publish(message, headers).ConfigureAwait(false);
    }

    private bool HasRoute(Type messageType) { ... }

    private Dictionary<string, string> BuildHeaders(MessageSendOptions options) { ... }
    private Dictionary<string, string> BuildHeaders(MessagePublishOptions options) { ... }
    private static void ValidateOptions(MessageSendOptions options) { ... }
    private static void ValidateOptions(MessagePublishOptions options) { ... }
}
```

Los overloads sin opciones delegan en los overloads con opciones usando `Default`. Por tanto, todas las validaciones y la construccion de headers se ejecutan siempre.

Logica de `BuildHeaders`: mergea los headers personalizados y, si `TimeToLive` no es null, anade el header `rbs2-time-to-be-received` con el formato `c` de `TimeSpan`. Si se superponen claves, el header personalizado tiene prioridad sobre el TTL.

Validaciones:
- `Delay` debe ser > `TimeSpan.Zero`.
- `TimeToLive` debe ser > `TimeSpan.Zero`.
- Si ambos se especifican, `TimeToLive` debe ser > `Delay`.
- Headers reservados de Rebus (14 constantes) no pueden sobrescribirse.

### RebusMessageHandlerAdapter

Adapta `IMessageHandler<T>` (contrato propio) a `IHandleMessages<T>` (contrato Rebus). Es interno: no se expone a la capa de aplicacion.

```csharp
internal sealed class RebusMessageHandlerAdapter<TMessage>(
    IMessageHandler<TMessage> handler) : IHandleMessages<TMessage>
    where TMessage : class
{
    public Task Handle(TMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return handler.HandleAsync(message, CancellationToken.None);
    }
}

internal sealed class RebusMessageHandlerAdapter<TMessage, THandler>(
    THandler handler) : IHandleMessages<TMessage>
    where TMessage : class
    where THandler : class, IMessageHandler<TMessage>
{
    public Task Handle(TMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return handler.HandleAsync(message, CancellationToken.None);
    }
}
```

La sobrecarga generica por tipo de handler se usa para evitar colisiones DI cuando hay multiples handlers para el mismo tipo de mensaje. El `CancellationToken` se pasa como `None` porque Rebus no propaga el token del bus a los handlers de mensajes.

### RebusSubscriptionHostedService

```csharp
internal sealed class RebusSubscriptionHostedService(
    IBus bus, RebusSubscriptionRegistry registry) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var eventType in registry.EventTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
                continue;

            await bus.Subscribe(eventType).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

Al arrancar la aplicacion, itera los tipos de evento detectados y llama a `bus.Subscribe()` para cada uno. Solo se registra si hay eventos que suscribir. Filtra por `IIntegrationEvent` como medida de seguridad adicional.

### RegisterHandlers (en RebusMessagingConfiguration)

```csharp
private static RebusSubscriptionRegistry RegisterHandlers(
    IServiceCollection services,
    MessagingSettings options,
    IReadOnlyCollection<Assembly> handlerAssemblies)
{
    var implementationTypes = ResolveAssemblies.ScanTypes(handlerAssemblies, IsConcreteType);
    var eventTypes = new HashSet<Type>();

    foreach (var implementationType in implementationTypes)
    {
        foreach (var serviceType in implementationType.ImplementedInterfaces
                     .Where(IsMessageHandlerType))
        {
            var messageType = serviceType.GetGenericArguments()[0];

            services.TryAdd(ServiceDescriptor.Transient(
                implementationType.AsType(), implementationType.AsType()));
            services.TryAddEnumerable(ServiceDescriptor.Transient(
                serviceType, implementationType.AsType()));
            services.TryAddEnumerable(
                CreateRebusAdapterDescriptor(serviceType, implementationType.AsType()));

            if (typeof(IIntegrationEvent).IsAssignableFrom(messageType))
                eventTypes.Add(messageType);
        }
    }

    return new RebusSubscriptionRegistry(eventTypes.ToArray());
}
```

Escanea los assemblies, detecta todas las implementaciones concretas de `IMessageHandler<T>`, las registra en DI y construye el registro de eventos para auto-subscribe. Usa `TryAdd`/`TryAddEnumerable` para no sobrescribir registros existentes.

### Metodos privados clave (ConsumerBase)

| Metodo | Funcion |
|---|---|
| `HandleCommandCore<TCommand>` | Ejecuta el comando via `dispatcher.Send`. Si `result.IsSuccess` es `false`, evalua `ShouldThrow`. |
| `ShouldThrow(Result<Unit>)` | Retorna `true` si el error es `Failure`, `Internal`, `Timeout` o `Unavailable`. Errores `Validation`, `NotFound`, `Conflict`, etc. se loguean y se consume el mensaje sin reintentar. |

### Configuracion de reintentos

En `RebusMessagingConfiguration`:

```csharp
configure = configure.Options(rebusOptions =>
{
    var errorQueueAddress = string.IsNullOrWhiteSpace(messagingOptions.ErrorQueueAddress)
        ? $"{messagingOptions.InputQueueName}.error"
        : messagingOptions.ErrorQueueAddress;

    rebusOptions.RetryStrategy(
        errorQueueName: errorQueueAddress,
        maxDeliveryAttempts: messagingOptions.MaxDeliveryAttempts);
});
```

La cola de errores es `{InputQueueName}.error` por defecto. Si se agotan los reintentos, el mensaje va a la cola de errores.

---

## Decision de envio: local vs remoto vs evento

| Tipo de mensaje | Route configurada | Delay | Metodo Rebus | Destino |
|---|---|---|---|---|---|
| `IIntegrationEvent` | N/A | No | `Publish()` | Todos los suscriptores (auto-subscribe) |
| `ICommandMessage` | No | No | `SendLocal()` | Misma aplicacion |
| `ICommandMessage` | No | Si | `DeferLocal()` | Misma aplicacion (diferido) |
| `ICommandMessage` | Si | No | `Send()` | Cola remota |
| `ICommandMessage` | Si | Si | `Defer()` | Cola remota (diferido) |

Regla practica:
- **Evento** (`IIntegrationEvent`): usa `PublishAsync`. El sistema se suscribe automaticamente.
- **Comando local** (`ICommandMessage` sin route): usa `SendAsync`.
- **Comando a worker** (`ICommandMessage` con route): usa `SendAsync`.
- **Comando diferido**: añade `WithDelay(...)` a las opciones.

El tipo del mensaje se valida en compile-time: `PublishAsync` solo acepta `IIntegrationEvent`, `SendAsync` solo acepta `ICommandMessage`. No hay fallback para tipos sin marker.

---

## Reintentos y errores

| ErrorType | Comportamiento |
|---|---|
| `Failure`, `Internal`, `Timeout`, `Unavailable` | `MessageHandlingException` → Rebus reintenta (hasta `MaxDeliveryAttempts`) |
| `Validation`, `NotFound`, `Conflict`, `Forbidden` | Se loguea como warning, se consume el mensaje sin reintentar |
| Exito (`IsSuccess = true`) | Se consume el mensaje |

El pipeline de reintentos lo gestiona Rebus con `SimpleRetryStrategy`. Si se agotan los intentos, el mensaje va a la cola de errores.

---

## Health Check

El componente registra un health check `RebusMessageHealthCheck` con el nombre `rebus_messaging` y tags `["messaging", "rebus"]`.

- Para transport InMemory: retorna `Healthy` directamente.
- Para RabbitMQ/Azure Service Bus: verifica `bus.Advanced.Workers.Count` como prueba de conectividad.
- Si la verificacion falla, retorna `Unhealthy`.

Se registra automaticamente al llamar a `AddRebusMessaging`:

```csharp
services.AddHealthChecks()
    .AddCheck<RebusMessageHealthCheck>("rebus_messaging", tags: ["messaging", "rebus"]);
```

Para consultarlo, usa el endpoint de health checks de ASP.NET Core. No requiere configuracion adicional.

---

## Consideraciones

### Routing y resolucion de tipos

Las routes en `MessagingSettings.Routes` se resuelven en este orden:
1. `Type.GetType(typeName, throwOnError: false)` — funciona con `AssemblyQualifiedName`.
2. `assembly.GetType(typeName, throwOnError: false)` para cada assembly escaneado — permite usar `FullName` sin ensamblado completo.

```json
"Routes": {
  "Akay.Be.Application.Features.Messaging.GenerateLearningHubReportMessage, Akay.Be.Application": "akaybe.worker"
}
```

Si el tipo no se puede resolver, `AddRebusMessaging` lanza `InvalidOperationException` al configurar Rebus.

### AssemblyQualifiedName recomendado para rutas

Para comandos remotos, usa `AssemblyQualifiedName` como clave de route. Es mas robusto que `FullName` y funciona independientemente del contexto de carga de assemblies. `FullName` solo funciona si el tipo esta en uno de los assemblies escaneados de handlers.

### AutoRegisterHandlers

Por defecto es `true`: todos los `IMessageHandler<T>` concretos en los assemblies pasados se registran. Si es `false`, debes usar `AddRebusHandler<TMessage, THandler>()` para registrar cada handler manualmente:

```csharp
services.AddRebusSettings(new MessagingSettings
{
    Transport = MessagingTransportNames.InMemory,
    InputQueueName = "my-queue",
    AutoRegisterHandlers = false
});

// Registro manual de handlers (obligatorio cuando AutoRegisterHandlers = false)
services.AddRebusHandler<LearningHubCreatedEvent, UserRegisteredConsumer>();
services.AddRebusHandler<GenerateLearningHubReportMessage, GenerateLearningHubReportConsumer>();
```

`AddRebusHandler` registra el handler en DI, crea el adaptador Rebus y lo vincula al mensaje. No requiere pasar assemblies de escaneo.

### Consumers en el assembly de Host

Los consumers viven en el proyecto Host (`Akay.Be.Host.Consumers`). Para que `ScanTypes` los encuentre, debes pasar `Assembly.GetEntryAssembly()` (o `typeof(UserRegisteredConsumer).Assembly`) como assembly de handlers.

### No hay sagas ni outbox

El wrapper actual no incluye soporte para sagas, outbox, ni transactional inbox. Si necesitas consistencia transaccional entre base de datos y mensajeria, implementalo fuera de esta abstraccion.

### InMemory solo para desarrollo y tests

El transport InMemory (`InMemNetwork`) es adecuado para desarrollo local y tests de integracion. No persiste mensajes entre reinicios y no soporta multiples procesos.

### Headers y cabeceras reservadas

Los headers personalizados se pasan directamente a Rebus como `IDictionary<string, string>` en el metodo de envio. Rebus anade sus propias cabeceras automaticamente (MessageId, Type, SentTime, etc.) en el pipeline de salida. Las claves reservadas de Rebus estan bloqueadas para evitar romper el comportamiento del bus.

Si necesitas correlacion, recomendamos usar un header propio (`x-correlation-id`) en lugar del header interno de Rebus (`CorrelationId` / `rbs2-correlation-id`) que Rebus gestiona automaticamente.

### SendLocal vs bus.Send con route

Si un mensaje tiene route configurada pero tambien existe un consumer local, el mensaje se envia al worker remoto y **no** se entrega localmente. La route tiene prioridad. Si quieres que se procese tanto local como remotamente, publica un `IIntegrationEvent` en lugar de usar un comando.

---

## Testing

### Tests de integracion (Akay.To.Core)

No se requieren emuladores ni Docker. Los tests de integracion usan transport InMemory:

```csharp
var services = new ServiceCollection();
services.AddLogging();

var settings = new MessagingSettings
{
    Transport = MessagingTransportNames.InMemory,
    InputQueueName = "test-integration"
};

services.AddRebusMessaging(settings, typeof(RebusMessagingIntegrationTests).Assembly);

await using var provider = services.BuildServiceProvider();

var bus = provider.GetRequiredService<IBus>();
await bus.Subscribe<IntegrationEventStub>();

var message = new IntegrationEventStub(Guid.NewGuid(), "Test Event");
await bus.Publish(message);

// Assert que el consumer recibio el mensaje
```

Para ejecutar los tests:

```powershell
dotnet test C:\Develop\Akay\Akay.To\Akay.To.Core\Akay.To.Core.Tests\Akay.To.Core.Tests.csproj
```

### Tests de integracion (Akay.Be)

Los tests de integracion en `Akay.Be.Host.Tests` prueban consumers reales con transport InMemory y un `RecordingDispatcher`:

```csharp
var dispatcher = new RecordingDispatcher();
services.AddSingleton<IDispatcher>(dispatcher);
services.AddRebusMessaging(settings, typeof(UserRegisteredConsumer).Assembly);

var bus = provider.GetRequiredService<IBus>();
await bus.Subscribe<LearningHubCreatedEvent>();

await bus.Publish(new LearningHubCreatedEvent(42, "Test Hub", "Desc"));

var command = await dispatcher.WaitForCommand<SendNewLearningHubNotification>(
    TimeSpan.FromSeconds(3));

Assert.Equal(42, command.Id);
```

Para ejecutar:

```powershell
dotnet test C:\Develop\Akay\Akay.Be\test\Akay.Be.Host.Tests\Akay.Be.Host.Tests.csproj
```

### Mock de IMessageBus

Para tests unitarios de consumidores de `IMessageBus`:

```csharp
var mockBus = new Mock<IMessageBus>();

mockBus.Setup(b => b.SendAsync(
        It.IsAny<GenerateLearningHubReportMessage>(),
        It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);

mockBus.Setup(b => b.PublishAsync(
        It.IsAny<LearningHubCreatedEvent>(),
        It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);

mockBus.Setup(b => b.SendAsync(
        It.Is<GenerateLearningHubReportMessage>(m => m.Id == 42),
        It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);

mockBus.Setup(b => b.PublishAsync(
        It.IsAny<LearningHubCreatedEvent>(),
        It.IsAny<CancellationToken>()))
    .Callback<LearningHubCreatedEvent, CancellationToken>((ev, _) =>
        Console.WriteLine($"Published event with Id={ev.Id}"));

mockBus.Setup(b => b.SendAsync(
        It.Is<object>(o => o is null),
        It.IsAny<CancellationToken>()))
    .ThrowsAsync(new ArgumentNullException());
```
