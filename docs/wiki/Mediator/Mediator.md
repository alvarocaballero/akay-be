# Mediator (Dispatcher)

## Qué es

`IDispatcher` e `IStreamDispatcher` son las interfaces públicas de un **mediator ligero y custom-built** que implementa el patrón Mediator sin dependencia de librerías externas como MediatR. Resuelve handlers por convención mediante DI y ejecuta una cadena de **pipeline behaviors** (logging, telemetría, validación, caché, compensación, retry) alrededor de cada request.

El dispatcher está diseñado para Clean Architecture con **CQRS implícito**: `ICommand` / `ICommand<T>` para escritura, `IQuery<T>` para lectura, ambos retornando `Result` o `Result<T>`.

**Paquete:** `Akay.To.Core`
**Namespace base:** `Akay.To.Core.Application.Mediator`
**Dispatcher:** `Akay.To.Core.Host.Mediator.IDispatcher`

---

## Por qué usarlo

- **Zero-dependency Mediator:** implementación propia sin MediatR, evitando dependencias externas y con control total del pipeline.
- **Pipeline behaviors componibles:** cada comportamiento (logging, telemetría, validación, caché, compensación, retry) se registra como `IPipelineBehavior<,>` y se encadena automáticamente en orden inverso.
- **Auto-descubrimiento de handlers:** escanea assemblies para registrar automáticamente `IRequestHandler<,>`, `IValidator<>`, `IPipelineBehavior<,>` y behaviors especializados (cache, retry, compensación).
- **Streaming nativo:** `IStreamDispatcher` con soporte para `IAsyncEnumerable<T>` y behaviors específicos de streaming.
- **Alto rendimiento:** caché de invoke delegates mediante `ConcurrentDictionary` + `MakeGenericMethod` para evitar reflection en caliente.

---

## Arquitectura

### Interfaces de request

```csharp
public interface IRequest<out TResponse>;

public interface ICommand : IRequest<Result>;
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;

public interface IQuery<TResponse> : IRequest<Result<TResponse>>;

public interface IStreamRequest<TResponse> : IRequest<IAsyncEnumerable<TResponse>>;
```

### Interfaces de handler

```csharp
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
```

### Dispatcher

```csharp
public interface IDispatcher
{
    ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

public interface IStreamDispatcher
{
    IAsyncEnumerable<TResponse> Stream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}
```

### Pipeline behavior

```csharp
internal delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();

internal interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
```

Cada behavior recibe el `next` delegate que representa el resto de la cadena. Se encadenan en orden inverso al registro, de modo que el primer behavior registrado es el más externo (outer middleware).

---

## Pipeline Behaviors

### Orden de ejecución

Al registrarse todos los behaviors vía `TryAddEnumerable`, el orden de ejecución es el siguiente (de más externo a más interno):

```
LoggingBehavior → TelemetryBehavior → ValidationBehavior → CacheBehavior → BlobCacheBehavior → CompensationBehavior → RetryBehavior → Handler
```

### LoggingBehavior

Registra `Debug` al entrar, `Debug` al completar, `Debug` al cancelar y `Error` ante excepciones no controladas.

```
ILogger<LoggingBehavior<TRequest, TResponse>> → LogDebug/LogError
```

### TelemetryBehavior

Crea una `Activity` de OpenTelemetry con nombre `"dispatcher.request"` y tags:
- `application.name`, `application.version`
- `request.type`, `request.pipeline=sync`
- `result.success`, `result.error.type`, `result.error.code`
- `request.cancelled`, `exception.type`, `exception.message`

### ValidationBehavior

Ejecuta todos los `IValidator<TRequest>` registrados. Si alguno falla, retorna `Result.Failure(error)` sin ejecutar el handler.

### RetryBehavior

Si el request implementa `IRetryableRequest`, aplica política **Polly WaitAndRetryAsync** con backoff exponencial:

```csharp
retryCount = request.RetryCount
delay = baseDelay * 2^(attempt-1)
```

### CacheBehavior

Si el request implementa `ICacheable<TValue>`, busca en caché antes del handler y almacena el resultado exitoso en caché después del handler.

### BlobCacheBehavior

Si el request implementa `IBlobCacheable<TValue>`, busca si el blob ya existe en Azure Blob Storage antes de ejecutar el handler. Si existe, devuelve la URI del blob sin ejecutar el handler (short-circuit). Si no existe, ejecuta el handler, que genera el archivo y lo sube al blob storage.

Este behavior está pensado para handlers que generan archivos costosos (PDFs, imágenes, SVGs, reportes) donde regenerar en cada petición es innecesario si el archivo ya existe.

**Flujo:**
1. Si `BypassBlobCache = true`, ejecuta el handler siempre (forzar regeneración).
2. Resuelve `IBlobStorageServiceFactory`, crea `IBlobStorageService` para `BlobContainerName`.
3. Verifica existencia con `blobStorage.ExistsAsync(request.BlobName)`.
4. Si existe → `Result.Success(request.CreateCachedValue(blobUri))` sin ejecutar handler.
5. Si no existe → ejecuta handler (que genera y sube el archivo).

**Resolución de concurrencia:**
Para carreras en miss concurrente (varios requests detectan ausencia y generan a la vez), usar `UploadOrGetUriAsync` en lugar de `UploadAsync` dentro del handler. Este método sube con `overwrite:false` y si hay `409 Conflict` (otro proceso ya subió), devuelve la URI existente en lugar de lanzar excepción.

### CompensationBehavior

Ejecuta automáticamente un stack LIFO de acciones de compensación cuando el handler falla (excepción o `Result.Failure`).

El handler registra acciones de compensación durante su ejecución normal mediante `ICompensationContext`:

```csharp
compensations.Add("Delete created hub",
    () => DeleteHubAsync(hubId));
compensations.Add("Rollback audit log",
    ct => RollbackAuditAsync(hubId, ct));
```

Si el handler completa con éxito, las compensaciones **no se ejecutan** (se descartan). Si el handler lanza una excepción o retorna `Result.Failure`, el behavior ejecuta todas las compensaciones registradas en orden **LIFO** (última en entrar, primera en salir).

Características:
- Cada compensación captura sus propias excepciones → una compensación fallida no impide ejecutar las demás.
- Tras ejecutarse (o descartarse), el stack se limpia automáticamente.
- Solo se activa en requests que implementan `ICompensableRequest`.
- El `ICompensationContext` se registra como **Scoped**; mismo ciclo de vida que el handler.

---

## Interfaces de extensión

### ICacheable / ICacheable\<T\>

```csharp
public interface ICacheable
{
    string CacheKey { get; }
    TimeSpan? CacheExpiration { get; }
}

public interface ICacheable<TValue> : ICacheable;
```

### IBlobCacheable / IBlobCacheable\<T\>

```csharp
public interface IBlobCacheable
{
    string BlobContainerName { get; }
    string BlobName { get; }
    bool BypassBlobCache => false;
}

public interface IBlobCacheable<TValue> : IBlobCacheable
{
    TValue CreateCachedValue(Uri blobUri);
}
```

Miembros:

| Miembro | Descripción |
|---|---|
| `BlobContainerName` | Nombre del contenedor Azure Blob Storage donde se almacena el archivo (ej. `"reports"`, `"learninghub-assets"`). |
| `BlobName` | Ruta del blob dentro del contenedor. Actúa como clave de caché. Incluir ID de entidad y tipo/versión para evitar colisiones (ej. `"hubs/{id}/badge.svg"`). |
| `BypassBlobCache` | Si `true`, fuerza la regeneración del archivo ignorando el blob existente. Útil para cambios de plantilla, branding o regeneración bajo demanda. Controlable desde el endpoint con un query param. |
| `CreateCachedValue(blobUri)` | Construye el valor de respuesta cuando el blob ya existe (hit de caché). Recibe la URI del blob y devuelve el tipo de respuesta del handler (ej. `blobUri.ToString()` para `string`). |

### IRetryableRequest

```csharp
public interface IRetryableRequest
{
    int RetryCount { get; }
    TimeSpan BaseDelay { get; }
}
```

### ICompensableRequest

```csharp
namespace Akay.To.Core.Application.Mediator;

public interface ICompensableRequest;
```

Marker interface. Al implementarla en un request, el `CompensationBehavior` se registra automáticamente en su pipeline. Si el handler no la implementa, el behavior no se instancia para ese request.

### ICompensationContext

```csharp
namespace Akay.To.Core.Application.Contexts;

public interface ICompensationContext
{
    bool HasCompensations { get; }

    void Add(Func<Task> compensation, string? name = null);

    void Add(Func<CancellationToken, ValueTask> compensation, string? name = null);

    ValueTask RunAsync(CancellationToken cancellationToken = default);

    void Clear();
}
```

Inyectable en cualquier handler como dependencia scoped. Métodos:

| Método | Descripción |
|---|---|
| `Add(func, name?)` | Apila una acción de compensación con nombre opcional (para trazabilidad). El nombre es solo informativo. |
| `HasCompensations` | `true` si hay acciones pendientes en el stack. |
| `RunAsync(ct)` | Ejecuta todas las compensaciones en orden LIFO. No lanza aunque una compensación falle. |
| `Clear()` | Descarta todas las compensaciones sin ejecutarlas. |

Normalmente no necesitas llamar a `RunAsync` ni `Clear` manualmente; el `CompensationBehavior` lo hace por ti.

---

## Configuración

### Registro básico

```csharp
using Akay.To.Core.Application.DependencyInjection;

services.AddDispatcher(assemblies: typeof(ApplicationRegisterModule).Assembly);
```

### Registro con opciones

```csharp
services.AddDispatcher(options =>
{
    options.UseStreaming = true;              // default: true
    options.UseLoggingBehavior = true;        // default: true
    options.UseTelemetryBehavior = true;      // default: true
    options.UseValidationBehavior = true;     // default: true
    options.UseRetryBehavior = true;          // default: true
    options.UseCacheBehavior = true;          // default: true
    options.UseBlobCacheBehavior = true;      // default: true
    options.UseCompensationBehavior = true;   // default: true
},
assemblies: typeof(ApplicationRegisterModule).Assembly);
```

### Qué registra en DI

| Servicio | Lifetime | Condición |
|---|---|---|
| `IDispatcher` → `Dispatcher` | Scoped | Siempre |
| `IStreamDispatcher` → `StreamDispatcher` | Scoped | `UseStreaming = true` |
| `IPipelineBehavior<,>` → `LoggingBehavior<,>` | Transient | `UseLoggingBehavior` |
| `IPipelineBehavior<,>` → `TelemetryBehavior<,>` | Transient | `UseTelemetryBehavior` |
| `IStreamPipelineBehavior<,>` → `StreamLoggingBehavior<,>` | Transient | `UseStreaming` |
| `IStreamPipelineBehavior<,>` → `StreamTelemetryBehavior<,>` | Transient | `UseStreaming` |
| `ICompensationContext` → `CompensationContext` | Scoped | `UseCompensationBehavior` |
| `IRequestHandler<,>` → implementaciones | Transient | Auto-descubierto |
| `IStreamRequestHandler<,>` → implementaciones | Transient | Auto-descubierto |
| `IValidator<>` → implementaciones | Transient | `UseValidationBehavior` |
| `IPipelineBehavior<,>` → `ValidationBehavior<,>` | Transient | `UseValidationBehavior` + `IValidator<TRequest>` |
| `IPipelineBehavior<,>` → `CacheBehavior<,>` | Transient | `UseCacheBehavior` + `ICacheable<T>` |
| `IPipelineBehavior<,>` → `BlobCacheBehavior<,>` | Transient | `UseBlobCacheBehavior` + `IBlobCacheable<T>` |
| `IPipelineBehavior<,>` → `CompensationBehavior<,>` | Transient | `UseCompensationBehavior` + `ICompensableRequest` |
| `IPipelineBehavior<,>` → `RetryBehavior<,>` | Transient | `UseRetryBehavior` + `IRetryableRequest` |

### Auto-descubrimiento

`AddDispatcher` escanea los assemblies proporcionados para registrar automáticamente:
- **Handlers:** cualquier clase concreta que implemente `IRequestHandler<,>` o `IStreamRequestHandler<,>`
- **Validators:** cualquier clase concreta que implemente `IValidator<>` (y genera `ValidationBehavior` para el request validado)
- **Cache behaviors:** cualquier request que implemente `ICacheable<T>` (genera y registra `CacheBehavior<TRequest, TValue>`)
- **Blob cache behaviors:** cualquier request que implemente `IBlobCacheable<T>` (genera y registra `BlobCacheBehavior<TRequest, TValue>`)
- **Compensation behaviors:** cualquier request que implemente `ICompensableRequest` (genera y registra `CompensationBehavior<TRequest, TResponse>`)
- **Retry behaviors:** cualquier request que implemente `IRetryableRequest` (genera y registra `RetryBehavior<TRequest, TResponse>`)
- **Custom behaviors:** cualquier clase concreta que implemente `IPipelineBehavior<,>` o `IStreamPipelineBehavior<,>`

Si no se pasan assemblies, se usan el entry assembly y el calling assembly.

---

## Implementación interna

### Dispatcher (sync)

`Dispatcher.cs:9-67` — Caché de invokers tipados mediante `ConcurrentDictionary<DispatcherCacheKey, DispatcherInvoker>`. Cada invoker:
1. Resuelve el `IRequestHandler<TRequest, TResponse>` del DI
2. Resuelve todos los `IPipelineBehavior<TRequest, TResponse>`, los invierte y los encadena
3. Ejecuta la cadena: behaviors envuelven al handler, el más externo se ejecuta primero

### StreamDispatcher (async stream)

`StreamDispatcher.cs:9-66` — Mismo patrón pero con `IStreamRequestHandler<,>` e `IStreamPipelineBehavior<,>`. Devuelve `IAsyncEnumerable<TResponse>` sin materializar.

---

## Ejemplos de uso

### Query handler

```csharp
public sealed record GetLearningHubQuery(int Id) : IQuery<LearningHubResponse>;

internal sealed class GetLearningHubQueryHandler : IQueryHandler<GetLearningHubQuery, LearningHubResponse>
{
    public ValueTask<Result<LearningHubResponse>> Handle(GetLearningHubQuery request, CancellationToken cancellationToken)
    {
        var hub = LearningHubStore.GetById(request.Id);
        return hub is null
            ? ValueTask.FromResult<Result<LearningHubResponse>>(
                Error.NotFound("learninghub.not_found", $"Centro con ID {request.Id} no encontrado."))
            : ValueTask.FromResult<Result<LearningHubResponse>>(
                new LearningHubResponse(hub.Id, hub.Name, ...));
    }
}
```

### Command handler con validación

```csharp
public sealed record CreateLearningHubCommand(string Name, string Description) : ICommand<LearningHubResponse>;

public sealed class CreateLearningHubCommandValidator : AbstractValidator<CreateLearningHubCommand>
{
    public CreateLearningHubCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}

internal sealed class CreateLearningHubCommandHandler : ICommandHandler<CreateLearningHubCommand, LearningHubResponse>
{
    public ValueTask<Result<LearningHubResponse>> Handle(CreateLearningHubCommand request, CancellationToken cancellationToken)
    {
        var hub = LearningHubStore.Add(request.Name, request.Description);
        return ValueTask.FromResult<Result<LearningHubResponse>>(
            new LearningHubResponse(hub.Id, hub.Name, ...));
    }
}
```

### Query con caché

```csharp
public sealed record GetCachedLearningHubQuery(int Id) : IQuery<LearningHubResponse>, ICacheable<LearningHubResponse>
{
    public string CacheKey => $"learninghub:{Id}";
    public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);
}
// El handler es el mismo, CacheBehavior intercepta antes/después automáticamente
```

### Query con Blob Cache (archivos)

Para handlers que generan archivos (PDFs, imágenes, SVGs) y los almacenan en Azure Blob Storage. El `BlobCacheBehavior` verifica si el archivo ya existe en el blob y, si es así, devuelve la URI sin ejecutar el handler.

```csharp
using System.Text;
using Akay.To.Core.Application.Abstractions.BlobStorage;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Results;

public sealed record GetLearningHubBadgeUriQuery(int Id, bool ForceRegenerate = false)
    : IQuery<string>, IBlobCacheable<string>
{
    // Contenedor donde se almacenan los assets generados
    public string BlobContainerName => "learninghub-assets";

    // Ruta del blob: {id}/badge.svg. Al incluir el ID, cada hub tiene su propio archivo.
    public string BlobName => $"hubs/{Id}/badge.svg";

    // Si ForceRegenerate = true (query param ?forceRegenerate=true), ignora el blob existente.
    public bool BypassBlobCache => ForceRegenerate;

    // Cuando el behavior detecta un hit, devuelve la URI del blob.
    public string CreateCachedValue(Uri blobUri) => blobUri.ToString();
}

internal sealed class GetLearningHubBadgeUriQueryHandler(IBlobStorageServiceFactory blobFactory)
    : IQueryHandler<GetLearningHubBadgeUriQuery, string>
{
    public async ValueTask<Result<string>> Handle(
        GetLearningHubBadgeUriQuery request, CancellationToken cancellationToken)
    {
        var hub = LearningHubStore.GetById(request.Id);
        if (hub is null)
            return Error.NotFound("learninghub.not_found",
                $"Centro con ID {request.Id} no encontrado.");

        // Crear servicio de blob para el contenedor configurado
        var blob = await blobFactory.CreateAsync("learninghub-assets",
            forceCreateContainer: true, cancellationToken: cancellationToken);

        // Generar contenido del archivo
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\">" +
                  $"<text>{hub.Name}</text></svg>";
        var bytes = Encoding.UTF8.GetBytes(svg);
        using var stream = new MemoryStream(bytes);

        // UploadOrGetUriAsync: sube con overwrite:false. Si hay 409 (otro proceso ya lo generó),
        // devuelve la URI existente en lugar de lanzar excepción. Resuelve carreras concurrentes.
        var uri = await blob.UploadOrGetUriAsync(request.BlobName, stream,
            contentType: "image/svg+xml",
            compress: false,
            cancellationToken: cancellationToken);

        return uri;
    }
}
```

**Controller endpoint:**

```csharp
[HttpGet("{id:int}/badge-uri")]
[AllowAnonymous]
public async Task<IResult> GetBadgeUri(int id, [FromQuery] bool forceRegenerate,
    CancellationToken cancellationToken) =>
    (await dispatcher.Send(new GetLearningHubBadgeUriQuery(id, forceRegenerate),
        cancellationToken)).ToOk();
```

**Comportamiento esperado:**
- Primera llamada (`GET /api/learning-hubs/1/badge-uri`): `BlobCacheBehavior` ve que el blob no existe → ejecuta handler → genera SVG y lo sube → devuelve URI.
- Segunda llamada: `BlobCacheBehavior` ve que el blob ya existe → devuelve URI directamente, sin ejecutar handler.
- Con `?forceRegenerate=true`: `BlobCacheBehavior` salta la verificación y ejecuta el handler siempre.
- Llamadas concurrentes durante la primera generación: `UploadOrGetUriAsync` captura `409 Conflict` y devuelve la URI del blob que otro proceso ya creó.

### Command con retry

```csharp
public sealed record SyncLearningHubCommand(int Id) : ICommand<LearningHubResponse>, IRetryableRequest
{
    public int RetryCount => 3;
    public TimeSpan BaseDelay => TimeSpan.FromMilliseconds(200);
}
// RetryBehavior reintenta hasta 3 veces con backoff exponencial: 200ms, 400ms, 800ms
```

### Command con compensaciones

```csharp
using Akay.To.Core.Application.Contexts;
using Akay.To.Core.Application.Mediator;

public sealed record CreateLearningHubWithNotificationCommand(
    string Name, string Description, string Address, string Category)
    : ICommand<LearningHubResponse>, ICompensableRequest;

internal sealed class CreateLearningHubWithNotificationCommandHandler(
    ICompensationContext compensations) : ICommandHandler<CreateLearningHubWithNotificationCommand, LearningHubResponse>
{
    public ValueTask<Result<LearningHubResponse>> Handle(
        CreateLearningHubWithNotificationCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Crear el recurso
        var created = LearningHubStore.Add(new LearningHubData(
            0, request.Name, request.Description,
            request.Address, request.Category,
            "active", DateTime.MinValue, DateTime.MinValue));

        // 2. Registrar compensación: si algo falla después, borrar lo creado
        compensations.Add(
            () => DeleteHubAsync(created.Id),
            $"Delete created hub '{created.Name}'");

        // 3. Operación que puede fallar
        SendWelcomeNotification(created);

        // El handler no necesita try/catch.
        // CompensationBehavior ejecuta las compensaciones automáticamente.
        return ValueTask.FromResult<Result<LearningHubResponse>>(
            new LearningHubResponse(created.Id, created.Name, ...));
    }

    private static void SendWelcomeNotification(LearningHubData hub)
    {
        // Si esto lanza, CompensationBehavior ejecuta la compensación
        // registrada en el paso 2 -> borra el hub.
        throw new InvalidOperationException("Notification failed.");
    }

    private static Task DeleteHubAsync(int hubId)
    {
        LearningHubStore.Delete(hubId);
        return Task.CompletedTask;
    }
}
```

El flujo es:

1. El handler crea el recurso y registra su compensación.
2. El handler ejecuta una operación que lanza excepción (o retorna `Error`).
3. `CompensationBehavior` detecta el fallo (`exception` o `Result.Failure`) y ejecuta el stack de compensaciones en orden LIFO.
4. Se relanza la excepción (o se propaga el error) hacia el controller.
5. Si el handler hubiera completado con éxito, las compensaciones se habrían descartado sin ejecutarse.

**Endpoint:** `POST api/learning-hubs/create-with-notification`

### Streaming

```csharp
public sealed record SearchLearningHubsStreamRequest(string? Keyword) : IStreamRequest<LearningHubStreamItem>;

internal sealed class SearchLearningHubsStreamHandler : IStreamRequestHandler<SearchLearningHubsStreamRequest, LearningHubStreamItem>
{
    public async IAsyncEnumerable<LearningHubStreamItem> Handle(
        SearchLearningHubsStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var hub in LearningHubStore.Search(request.Keyword))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new LearningHubStreamItem(hub.Id, hub.Name, ...);
            await Task.Delay(100, cancellationToken); // simula streaming
        }
    }
}
```

### Uso en controlador

```csharp
[ApiController]
[Route("api/learning-hubs")]
public sealed class LearningHubController(IDispatcher dispatcher, IStreamDispatcher streamDispatcher) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IResult> GetById(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new GetLearningHubQuery(id), cancellationToken)).ToOk();

    [HttpPost]
    public async Task<IResult> Create([FromBody] CreateLearningHubCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command, cancellationToken)).ToCreated(value => $"api/learning-hubs/{value.Id}");

    [HttpPost("create-with-notification")]
    public async Task<IResult> CreateWithNotification(
        [FromBody] CreateLearningHubWithNotificationCommand command, CancellationToken cancellationToken) =>
        (await dispatcher.Send(command, cancellationToken)).ToCreated(value => $"api/learning-hubs/{value.Id}");

    [HttpDelete("{id:int}")]
    public async Task<IResult> Delete(int id, CancellationToken cancellationToken) =>
        (await dispatcher.Send(new DeleteLearningHubCommand(id), cancellationToken)).ToNoContent();

    [HttpPost("search-stream")]
    public IAsyncEnumerable<LearningHubStreamItem> SearchStream(
        [FromBody] SearchLearningHubsStreamRequest request, CancellationToken cancellationToken) =>
        streamDispatcher.Stream(request, cancellationToken);
}
```

---

## Custom Pipeline Behaviors

Para añadir un behavior propio, implementa `IPipelineBehavior<,>`:

```csharp
internal sealed class PerformanceTimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next().ConfigureAwait(false);
        sw.Stop();

        // logging, métricas, etc.
        return response;
    }
}
```

El behavior se registra automáticamente si está en uno de los assemblies escaneados, o manualmente:

```csharp
services.TryAddEnumerable(ServiceDescriptor.Transient(
    typeof(IPipelineBehavior<,>), typeof(PerformanceTimingBehavior<,>)));
```

---

## Testing

### Test de handler (unitario, sin dispatcher)

```csharp
var handler = new GetLearningHubQueryHandler();
var result = await handler.Handle(new GetLearningHubQuery(1), CancellationToken.None);

result.Match(
    onSuccess: hub => Assert.Equal(1, hub.Id),
    onFailure: error => Assert.Fail($"Unexpected error: {error.Code}")
);
```

### Test con dispatcher (integración)

```csharp
var services = new ServiceCollection();
services.AddDispatcher(assemblies: typeof(GetLearningHubQuery).Assembly);
// ... registrar dependencias necesarias
var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<IDispatcher>();

var result = await dispatcher.Send(new GetLearningHubQuery(1));
Assert.True(result.IsSuccess);
```
