# Serilog

## Qué es

La integración de Serilog en `Akay.To.Core` proporciona **structured logging** como proveedor de logging principal de la aplicación, reemplazando el logger por defecto de .NET. Se configura mediante `AddObservability()` y se enriquece con enrichers estándar (máquina, proceso, hilo, excepciones) más propiedades personalizadas de la aplicación.

Todo el pipeline del dispatcher emite logs estructurados a través de `ILogger<T>` en `LoggingBehavior` y `StreamLoggingBehavior`.

**Paquete:** `Akay.To.Core`
**Método de registro:** `builder.AddObservability()` → `builder.Host.UseSerilog()`
**Namespace:** `Akay.To.Core.Host.DependencyInjection`

---

## Por qué usarlo

- **Structured logging:** los logs son objetos JSON estructurados, no strings planos, facilitando consultas en sinks como Elasticsearch, Seq o Datadog.
- **Configuración por appsettings:** niveles de log y sinks se configuran desde `appsettings.json` sin recompilar.
- **Enriquecimiento automático:** MachineName, ThreadId, ProcessId, ExceptionDetails y Application se añaden a todos los eventos.
- **Integración con ILogger\<T\>:** compatible con todo el ecosistema `Microsoft.Extensions.Logging`, permitiendo inyectar `ILogger<T>` en cualquier servicio.

---

## Arquitectura

### Registro

`HostRegisterExtensions.AddObservability()` (línea 101-122) configura Serilog mediante `UseSerilog()`:

```csharp
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .Enrich.WithProcessId()
        .Enrich.WithExceptionDetails()
        .Enrich.WithProperty("Application", serviceName);

    if (!string.IsNullOrWhiteSpace(correlationHeader))
        loggerConfiguration.Enrich.WithProperty("CorrelationHeader", correlationHeader);

    if (enableSerilogConsole)
        loggerConfiguration.WriteTo.Console();
});
```

### Enrichers

| Enricher | Paquete NuGet | Propiedad añadida |
|---|---|---|
| `FromLogContext()` | `Serilog` (core) | Propiedades del `LogContext` actual |
| `WithMachineName()` | `Serilog.Enrichers.Environment` | `MachineName` |
| `WithThreadId()` | `Serilog.Enrichers.Thread` | `ThreadId` |
| `WithProcessId()` | `Serilog.Enrichers.Process` | `ProcessId` |
| `WithExceptionDetails()` | `Serilog.Exceptions` | Desglose completo de excepción (tipo, stack trace, inner exceptions, data) |
| `WithProperty("Application", ...)` | `Serilog` (core) | `Application` = nombre del servicio |

---

## Configuración

### appsettings.json

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

### appsettings.Development.json

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.AspNetCore": "Information",
        "System": "Information"
      }
    }
  }
}
```

### Niveles de log por categoría

Los niveles se configuran con overrides por namespace:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Akay.To.Core": "Debug",
        "Akay.Be.Application": "Debug"
      }
    }
  }
}
```

### ObservabilityOptions

```csharp
builder.AddObservability("MyApp", "X-Correlation-Id", options =>
{
    options.EnableSerilog = true;
    options.EnableSerilogConsole = true;  // forzar consola incluso en producción
});
```

### NuGet packages

| Paquete | Versión | Propósito |
|---|---|---|
| `Serilog.AspNetCore` | 10.0.0 | Integración con ASP.NET Core (`UseSerilog()`) |
| `Serilog.Enrichers.Environment` | 3.0.1 | `WithMachineName()` |
| `Serilog.Enrichers.Process` | 3.0.0 | `WithProcessId()` |
| `Serilog.Enrichers.Thread` | 4.0.0 | `WithThreadId()` |
| `Serilog.Exceptions` | 8.4.0 | `WithExceptionDetails()` |
| `Serilog.Sinks.Console` | 6.1.1 | `WriteTo.Console()` |

---

## Logs del pipeline del Dispatcher

### LoggingBehavior (sync)

```csharp
logger.LogDebug("Handling request {RequestType}.", requestType);
// ... ejecución del handler ...
logger.LogDebug("Handled request {RequestType}.", requestType);
// cancelación:
logger.LogDebug("Request {RequestType} was cancelled.", requestType);
// error:
logger.LogError(exception, "Request {RequestType} failed with an unhandled exception.", requestType);
```

### StreamLoggingBehavior (streaming)

```csharp
logger.LogDebug("Starting stream for {RequestType}.", requestType);
// ... iteración del stream ...
logger.LogDebug("Stream completed for {RequestType}.", requestType);
// cancelación:
logger.LogDebug("Stream for {RequestType} was cancelled.", requestType);
// error:
logger.LogError(exception, "Stream for {RequestType} failed with an unhandled exception.", requestType);
```

---

## Ejemplos de uso

### Logging en un handler

```csharp
internal sealed class CreateLearningHubCommandHandler(ILogger<CreateLearningHubCommandHandler> logger)
    : ICommandHandler<CreateLearningHubCommand, LearningHubResponse>
{
    public ValueTask<Result<LearningHubResponse>> Handle(CreateLearningHubCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating learning hub {Name}", request.Name);

        var hub = LearningHubStore.Add(request.Name, request.Description);

        logger.LogInformation("Learning hub created with ID {HubId}", hub.Id);

        return ValueTask.FromResult<Result<LearningHubResponse>>(
            new LearningHubResponse(hub.Id, hub.Name, ...));
    }
}
```

### Logging con scopes

```csharp
using (logger.BeginScope(new Dictionary<string, object>
{
    ["UserId"] = userContext.UserId,
    ["OperationId"] = Guid.NewGuid()
}))
{
    logger.LogInformation("Processing order {OrderId}", orderId);
    // Todas las propiedades del scope se añaden a los eventos dentro del bloque
}
```

### Añadir sinks adicionales

Para añadir sinks como Seq, Elasticsearch, o Azure Application Insights en Producción:

```csharp
// En Program.cs, después de AddObservability()
if (!builder.Environment.IsDevelopment())
{
    builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    {
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .Enrich.WithExceptionDetails()
            .Enrich.WithProperty("Application", "MyApp")
            .WriteTo.Console()
            .WriteTo.Seq("http://seq-server:5341");           // Seq
            // .WriteTo.Elasticsearch(...)                     // Elasticsearch
            // .WriteTo.ApplicationInsights(...)                // Azure
    });
}
```

Nota: en producción, típicamente se usaría solo `appsettings.json` para configurar los sinks sin código adicional:

```json
{
  "Serilog": {
    "MinimumLevel": { "Default": "Information" },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "Seq",
        "Args": { "serverUrl": "http://seq-server:5341" }
      }
    ]
  }
}
```

### Estructura de un evento de log

Un evento de log de `LoggingBehavior` se ve así en consola:

```
[09:30:15 DBG] Handling request Akay.Be.Application.Features.LearningHubs.GetLearningHubQuery.
[09:30:15 DBG] Handled request Akay.Be.Application.Features.LearningHubs.GetLearningHubQuery.
```

Con propiedades enriquecidas (visibles en sinks estructurados como JSON):

```json
{
  "@t": "2025-01-15T09:30:15.0000000Z",
  "@mt": "Handling request {RequestType}.",
  "RequestType": "Akay.Be.Application.Features.LearningHubs.GetLearningHubQuery",
  "Application": "Akay.Be",
  "MachineName": "DEV-MACHINE",
  "ProcessId": 12345,
  "ThreadId": 7,
  "@l": "Debug"
}
```

---

## Testing

### Verificar logs emitidos

```csharp
var logger = new Mock<ILogger<GetLearningHubQueryHandler>>();
var handler = new GetLearningHubQueryHandler(logger.Object);

await handler.Handle(new GetLearningHubQuery(1), CancellationToken.None);

logger.Verify(
    x => x.Log(
        LogLevel.Debug,
        It.IsAny<EventId>(),
        It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Handling")),
        It.IsAny<Exception>(),
        It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
    Times.Never);  // LoggingBehavior usa ILogger<LoggingBehavior<,>>, no el del handler
```

### Verificar logs del pipeline (test de integración)

```csharp
// Usando MEL (Microsoft.Extensions.Logging) con Serilog
var services = new ServiceCollection();
services.AddLogging();
services.AddDispatcher(typeof(MyQuery).Assembly);

var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<IDispatcher>();

var result = await dispatcher.Send(new MyQuery());
// Los logs de LoggingBehavior se emiten automáticamente
```
