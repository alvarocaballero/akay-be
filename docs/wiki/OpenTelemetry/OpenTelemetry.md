# OpenTelemetry

## Qué es

La integración de OpenTelemetry en `Akay.To.Core` proporciona **tracing distribuido** para todas las operaciones del dispatcher (síncronas y streaming) y para el pipeline HTTP de ASP.NET Core. Se basa en `System.Diagnostics.ActivitySource` y se configura mediante `AddObservability()`.

Cada request del dispatcher genera una **Activity** con tags enriquecidos: nombre de la aplicación, versión, tipo de request, resultado (éxito/fallo), errores y estado de cancelación.

**Paquete:** `Akay.To.Core`
**ActivitySource:** `Akay.To.Core` (definido en `ApplicationDiagnostics.cs`)
**Namespace:** `Akay.To.Core.Shared.Diagnostics`

---

## Por qué usarlo

- **Trazabilidad completa:** cada request del dispatcher genera una Activity que puede correlarse con el tracing de ASP.NET Core, formando una traza distribuida de extremo a extremo.
- **Tags semánticos:** `result.success`, `result.error.type`, `result.error.code` permiten filtrar y analizar fallos en herramientas de observabilidad.
- **Zero-touch para developers:** no hay que instrumentar manualmente; `TelemetryBehavior` y `StreamTelemetryBehavior` se ejecutan automáticamente en el pipeline.
- **Exportable:** compatible con cualquier exporter de OpenTelemetry (Console, Jaeger, Zipkin, OTLP, Azure Monitor, etc.).

---

## Arquitectura

### ActivitySource

```csharp
public static class ApplicationDiagnostics
{
    public const string ActivitySourceName = "Akay.To.Core";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
```

### Actividades generadas

| Activity Name | Tipo de pipeline | Origen |
|---|---|---|
| `dispatcher.request` | Sync (`IDispatcher.Send`) | `TelemetryBehavior` |
| `dispatcher.stream` | Streaming (`IStreamDispatcher.Stream`) | `StreamTelemetryBehavior` |
| `Microsoft.AspNetCore.Hosting.HttpRequestIn` | HTTP Request | `AddAspNetCoreInstrumentation()` |

---

## Tags de telemetría

### dispatcher.request (sync)

| Tag | Tipo | Descripción |
|---|---|---|
| `application.name` | `string` | Nombre de la aplicación desde `BaseApplicationSettings` |
| `application.version` | `string` | Versión de la aplicación |
| `request.type` | `string` | `FullName` del tipo de request |
| `request.pipeline` | `string` | `"sync"` |
| `result.success` | `bool` | `true` si el `Result` es éxito |
| `result.error.type` | `string` | `ErrorType` como string (solo si fallo) |
| `result.error.code` | `string` | Código del error (solo si fallo) |
| `request.cancelled` | `bool` | `true` si se canceló vía `CancellationToken` |
| `exception.type` | `string` | Tipo de excepción no controlada |
| `exception.message` | `string` | Mensaje de la excepción |

### dispatcher.stream (streaming)

| Tag | Tipo | Descripción |
|---|---|---|
| `application.name` | `string` | Nombre de la aplicación |
| `application.version` | `string` | Versión de la aplicación |
| `request.type` | `string` | `FullName` del tipo de request |
| `request.pipeline` | `string` | `"stream"` |
| `stream.completed` | `bool` | `true` si el stream terminó sin cancelación |
| `stream.cancelled` | `bool` | `true` si se canceló |
| `stream.item.count` | `int` | Número de items emitidos por el stream |

### Tags de Resource

| Tag | Descripción |
|---|---|
| `service.name` | Nombre del servicio |
| `correlation.header` | Nombre del header de correlación (si configurado) |

---

## Extracción de tags de Result

`ResultTelemetry` utiliza `ResultAccessor<TResponse>` con **compiled expressions** para leer `IsSuccess` y `Error` del `Result<T>` sin boxing ni reflection en caliente.

```csharp
// ResultAccessor<TResponse> compila en static constructor:
isSuccess = Expression.Lambda<Func<TResponse, bool>>(
    Expression.Property(parameter, isSuccessProperty), parameter).Compile();
```

Esto permite inspeccionar cualquier `Result<T>` genérico con máximo rendimiento.

---

## Configuración

### Registro básico

```csharp
builder.AddObservability(applicationName: "MyApp", correlationHeader: "X-Correlation-Id");
```

### Qué configura

`AddObservability()` realiza dos acciones en un solo método:

1. **Serilog** (si `EnableSerilog = true`)
2. **OpenTelemetry** (si `EnableTracing = true`)

La parte de OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
    {
        resource.AddService(serviceName);
        if (!string.IsNullOrWhiteSpace(correlationHeader))
            resource.AddAttributes([
                new KeyValuePair<string, object>("correlation.header", correlationHeader)
            ]);
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource(ApplicationDiagnostics.ActivitySourceName);
        tracing.SetErrorStatusOnException();

        if (options.EnableAspNetCoreInstrumentation)
            tracing.AddAspNetCoreInstrumentation();

        if (enableConsoleExporter)
            tracing.AddConsoleExporter();
    });
```

### ObservabilityOptions

```csharp
public sealed class ObservabilityOptions
{
    public bool EnableSerilog { get; set; } = true;
    public bool EnableTracing { get; set; } = true;
    public bool EnableAspNetCoreInstrumentation { get; set; } = true;
    public bool? EnableConsoleExporter { get; set; }      // null = auto: true en Development
    public bool? EnableSerilogConsole { get; set; }       // null = auto: true en Development
}
```

### Configuración avanzada

```csharp
builder.AddObservability("MyApp", "X-Correlation-Id", options =>
{
    options.EnableTracing = true;
    options.EnableAspNetCoreInstrumentation = false;  // deshabilitar instrumentación HTTP
    options.EnableConsoleExporter = !builder.Environment.IsProduction();
});
```

### NuGet packages

| Paquete | Versión |
|---|---|
| `OpenTelemetry.Extensions.Hosting` | 1.15.3 |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.15.2 |
| `OpenTelemetry.Exporter.Console` | 1.15.3 |

---

## Flujo de tracing

```
HTTP Request → ASP.NET Core Instrumentation (Activity: HttpRequestIn)
    → IDispatcher.Send()
        → TelemetryBehavior (Activity: dispatcher.request, Parent: HttpRequestIn)
            → ValidationBehavior
            → RetryBehavior
            → CacheBehavior
            → Handler
        ← Tags de resultado se escriben en la Activity
    ← Activity de ASP.NET Core se completa
→ Console Exporter / OTLP Exporter exporta las spans
```

Cada `Activity` del dispatcher es hija de la `Activity` de ASP.NET Core, permitiendo ver la traza completa en herramientas como Jaeger, Zipkin o Azure Monitor.

---

## Ejemplos de uso

### Ver Activities en consola (Development)

En entorno `Development`, el Console Exporter se habilita automáticamente. Cada request imprime:

```
Activity.TraceId:            54d3f1c2a8b7e9d0f1a2b3c4d5e6f7a8
Activity.SpanId:             a1b2c3d4e5f6a7b8
Activity.TraceFlags:         Recorded
Activity.ActivitySourceName: Akay.To.Core
Activity.DisplayName:        dispatcher.request
Activity.Kind:               Internal
Activity.StartTime:          2025-01-15T10:30:00.0000000Z
Activity.Duration:           00:00:00.0150000
Activity.Tags:
    application.name: MyApp
    application.version: 1.0.0
    request.type: Akay.Be.Application.Features.LearningHubs.GetLearningHubQuery
    request.pipeline: sync
    result.success: True
Resource associated with Activity:
    service.name: MyApp
```

### Añadir exporter OTLP (producción)

```csharp
// En Program.cs, después de AddObservability
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddOtlpExporter());
```

### Añadir métricas

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddRuntimeInstrumentation();
    });
```

### Custom Activity en un handler

Para añadir spans personalizadas dentro de un handler:

```csharp
internal sealed class MyHandler : IQueryHandler<MyQuery, MyResponse>
{
    public async ValueTask<Result<MyResponse>> Handle(MyQuery request, CancellationToken cancellationToken)
    {
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity("my.custom.operation");
        activity?.SetTag("my.tag", "value");

        // ... lógica de negocio

        return Result<MyResponse>.Success(response);
    }
}
```

---

## Testing

### Verificar que se generan Activities

```csharp
using var activityListener = new ActivityListener
{
    ShouldListenTo = source => source.Name == ApplicationDiagnostics.ActivitySourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStarted = activity => testOutputHelper.WriteLine($"Started: {activity.DisplayName}"),
    ActivityStopped = activity =>
    {
        testOutputHelper.WriteLine($"Stopped: {activity.DisplayName}");
        Assert.Contains(activity.TagObjects, t => t.Key == "result.success");
    }
};
ActivitySource.AddActivityListener(activityListener);

// Ejecutar request...
var result = await dispatcher.Send(new MyQuery());
```

### Simular Activity nula (sin listener)

`TelemetryBehavior` maneja correctamente el caso donde `ActivitySource.StartActivity()` retorna `null` (cuando no hay listeners configurados). Los tags se omiten sin error.
