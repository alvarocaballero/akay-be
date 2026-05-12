# Mediator (Dispatcher)

## Qué es

`IDispatcher` e `IStreamDispatcher` son las interfaces públicas de un **mediator ligero y custom-built** que implementa el patrón Mediator sin dependencia de librerías externas como MediatR. Resuelve handlers por convención mediante DI y ejecuta una cadena de **pipeline behaviors** (logging, telemetría, validación, retry, caché) alrededor de cada request.

El dispatcher está diseñado para Clean Architecture con **CQRS implícito**: `ICommand` / `ICommand<T>` para escritura, `IQuery<T>` para lectura, ambos retornando `Result` o `Result<T>`.

**Paquete:** `Akay.To.Core`
**Namespace base:** `Akay.To.Core.Application.Mediator`
**Dispatcher:** `Akay.To.Core.Host.Mediator.IDispatcher`

---

## Por qué usarlo

- **Zero-dependency Mediator:** implementación propia sin MediatR, evitando dependencias externas y con control total del pipeline.
- **Pipeline behaviors componibles:** cada comportamiento (logging, telemetría, validación, retry, caché) se registra como `IPipelineBehavior<,>` y se encadena automáticamente en orden inverso.
- **Auto-descubrimiento de handlers:** escanea assemblies para registrar automáticamente `IRequestHandler<,>`, `IValidator<>`, `IPipelineBehavior<,>` y cache behaviors.
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
LoggingBehavior → TelemetryBehavior → ValidationBehavior → RetryBehavior → CacheBehavior → Handler
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

### IRetryableRequest

```csharp
public interface IRetryableRequest
{
    int RetryCount { get; }
    TimeSpan BaseDelay { get; }
}
```

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
    options.UseStreaming = true;            // default: true
    options.UseLoggingBehavior = true;      // default: true
    options.UseTelemetryBehavior = true;    // default: true
    options.UseValidationBehavior = true;   // default: true
    options.UseRetryBehavior = true;        // default: true
    options.UseCacheBehavior = true;        // default: true
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
| `IPipelineBehavior<,>` → `ValidationBehavior<,>` | Transient | `UseValidationBehavior` |
| `IPipelineBehavior<,>` → `RetryBehavior<,>` | Transient | `UseRetryBehavior` |
| `IStreamPipelineBehavior<,>` → `StreamLoggingBehavior<,>` | Transient | `UseStreaming` |
| `IStreamPipelineBehavior<,>` → `StreamTelemetryBehavior<,>` | Transient | `UseStreaming` |
| `IRequestHandler<,>` → implementaciones | Transient | Auto-descubierto |
| `IStreamRequestHandler<,>` → implementaciones | Transient | Auto-descubierto |
| `IValidator<>` → implementaciones | Transient | Auto-descubierto |
| `IPipelineBehavior<,>` → `CacheBehavior<,>` | Transient | Auto-descubierto (solo requests `ICacheable<T>`) |

### Auto-descubrimiento

`AddDispatcher` escanea los assemblies proporcionados para registrar automáticamente:
- **Handlers:** cualquier clase concreta que implemente `IRequestHandler<,>` o `IStreamRequestHandler<,>`
- **Validators:** cualquier clase concreta que implemente `IValidator<>`
- **Cache behaviors:** cualquier request que implemente `ICacheable<T>` (genera y registra `CacheBehavior<TRequest, TValue>`)
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

### Command con retry

```csharp
public sealed record SyncLearningHubCommand(int Id) : ICommand<LearningHubResponse>, IRetryableRequest
{
    public int RetryCount => 3;
    public TimeSpan BaseDelay => TimeSpan.FromMilliseconds(200);
}
// RetryBehavior reintenta hasta 3 veces con backoff exponencial: 200ms, 400ms, 800ms
```

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
