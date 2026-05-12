# HybridCache

## Qué es

`IHybridCacheService` es una abstracción de caché de dos niveles (L1: in-memory, L2: Redis) construida sobre `Microsoft.Extensions.Caching.Hybrid.HybridCache`. Proporciona una API unificada para operaciones de caché con expiración independiente por nivel, y se integra automáticamente en el pipeline del dispatcher mediante `CacheBehavior` para requests que implementan `ICacheable<T>`.

**Paquete:** `Akay.To.Core`
**Interfaz:** `Akay.To.Core.Application.Caching.IHybridCacheService`
**Implementación:** `Akay.To.Core.Infrastructure.Caching.HybridCacheService`
**Pipeline behavior:** `Akay.To.Core.Application.Mediator.Behaviors.CacheBehavior`

---

## Por qué usarlo

- **Caché de dos niveles:** L1 en memoria (rápida, corta duración) y L2 en Redis (compartida entre instancias, mayor duración), combinando velocidad y escalabilidad horizontal.
- **Integración transparente con el dispatcher:** solo hay que implementar `ICacheable<T>` en el request; `CacheBehavior` intercepta antes/después del handler automáticamente.
- **Expiración independiente por nivel:** `Expiration` para el nivel local (in-memory) y `LocalCacheExpiration` para el nivel distribuido (Redis), siguiendo las opciones de `HybridCacheEntryOptions`.
- **Health check incluido:** `CacheHealthCheck` verifica que la caché funciona correctamente escribiendo y leyendo una clave de prueba.

---

## Arquitectura

### IHybridCacheService

```csharp
public readonly record struct CacheLookup<TValue>(bool Found, TValue? Value);

public interface IHybridCacheService
{
    ValueTask<TValue> GetOrCreateAsync<TValue>(
        string key,
        Func<CancellationToken, ValueTask<TValue>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default);

    ValueTask<CacheLookup<TValue>> GetAsync<TValue>(
        string key,
        CancellationToken cancellationToken = default);

    ValueTask SetAsync<TValue>(
        string key,
        TValue value,
        TimeSpan? expiration,
        CancellationToken cancellationToken = default);
}
```

### HybridCacheService

Implementación que envuelve `Microsoft.Extensions.Caching.Hybrid.HybridCache`:

- **`GetOrCreateAsync`:** llama a `HybridCache.GetOrCreateAsync()` con `HybridCacheEntryOptions` opcionales para expiración. Si no se especifica expiración, usa los defaults configurados.
- **`GetAsync`:** implementa un lookup sin factory usando un truco: crea una entrada con `CacheEntry<TValue>.Miss` y expiración de 1ms. Si la clave existe, `HybridCache` devuelve el valor real ignorando la factory. Si no existe, devuelve `CacheLookup<TValue>(false, default)`.
- **`SetAsync`:** envuelve el valor en `CacheEntry<TValue>.ForValue(value)` y llama a `HybridCache.SetAsync()`.

### ICacheable / ICacheable\<T\>

```csharp
public interface ICacheable
{
    string CacheKey { get; }
    TimeSpan? CacheExpiration { get; }
}

public interface ICacheable<TValue> : ICacheable;
```

Cuando un request implementa `ICacheable<TValue>`, el `CacheBehavior` se registra automáticamente para ese par `(TRequest, Result<TValue>)`.

### CacheBehavior

`CacheBehavior<TRequest, TValue>` intercepta el pipeline:

1. **Antes del handler:** busca en caché con `cache.GetAsync<TValue>(request.CacheKey)`. Si encuentra (`Found == true`), retorna `Result<TValue>.Success(cached.Value)` sin ejecutar el handler.
2. **Después del handler:** si el handler retorna éxito (`TryGetValue` extrae el valor), almacena el valor en caché con `cache.SetAsync(request.CacheKey, value, request.CacheExpiration)`.

```csharp
internal sealed class CacheBehavior<TRequest, TValue>(IHybridCacheService cache)
    : IPipelineBehavior<TRequest, Result<TValue>>
    where TRequest : IRequest<Result<TValue>>, ICacheable<TValue>
{
    public async ValueTask<Result<TValue>> Handle(TRequest request, ...)
    {
        var cached = await cache.GetAsync<TValue>(request.CacheKey, ...);
        if (cached.Found)
            return Result<TValue>.Success(cached.Value);

        var result = await next();

        if (result.TryGetValue(out var value))
            await cache.SetAsync(request.CacheKey, value, request.CacheExpiration, ...);

        return result;
    }
}
```

---

## Configuración

### Registro en DI

```csharp
using Akay.To.Core.Infrastructure.DependencyInjection;

services.AddCache(settings);
```

### Qué registra

| Servicio | Lifetime | Condición |
|---|---|---|
| `IHybridCacheService` → `HybridCacheService` | Singleton | Siempre |
| `Microsoft.Extensions.Caching.Hybrid.HybridCache` | Singleton | Siempre (vía `AddHybridCache()`) |
| `IDistributedCache` → Redis | Singleton | Solo si `Cache.ConnectionString` tiene valor |

### CacheSettings

```csharp
public class CacheSettings
{
    public string? ConnectionString { get; set; }        // Redis (null = sin L2)
    public TimeSpan? RedisCacheExpiration { get; set; }  // Default: 30 min
    public TimeSpan? InMemoryCacheExpiration { get; set; } // Default: 5 min
    public int MaximumPayloadBytes { get; set; } = 1048576; // 1 MB
}
```

### Configuración en appsettings.json

```json
{
  "Cache": {
    "ConnectionString": "localhost:6379",
    "RedisCacheExpiration": "00:30:00",
    "InMemoryCacheExpiration": "00:05:00",
    "MaximumPayloadBytes": 1048576
  }
}
```

Si `ConnectionString` es `null` o vacío, no se registra Redis y solo funciona la caché en memoria (L1).

---

## Estrategia de dos niveles (L1 + L2)

| Nivel | Backend | Duración típica | Configuración |
|---|---|---|---|
| **L1 (local)** | `System.Runtime.Caching.MemoryCache` | 5 min | `InMemoryCacheExpiration` |
| **L2 (distribuida)** | Redis (StackExchange) | 30 min | `RedisCacheExpiration` |

Cuando se solicita una clave:
1. `HybridCache` busca en L1 (memoria local). Si existe, retorna inmediatamente.
2. Si no está en L1, busca en L2 (Redis). Si existe, la carga en L1 y retorna.
3. Si no está en L1 ni L2, ejecuta la factory, almacena en L1 y L2, y retorna.

### Opciones por operación (GetOrCreateAsync / SetAsync)

Cuando se pasa `expiration` explícito, se usa el mismo valor tanto para `Expiration` (L2) como `LocalCacheExpiration` (L1), ya que `HybridCacheService` los iguala:

```csharp
var options = expiration is null ? null : new HybridCacheEntryOptions
{
    Expiration = expiration,
    LocalCacheExpiration = expiration
};
```

---

## Health Check

`CacheHealthCheck` implementa `IHealthCheck` y verifica que la caché funciona:

1. Escribe una clave de prueba (`HealthCheckTestKey`) con `expiration: TimeSpan.Zero`
2. Lee la clave y verifica que el valor coincide
3. Retorna `Healthy` si funciona, `Degraded` si el valor no coincide, `Unhealthy` si lanza excepción

Nota: `TimeSpan.Zero` hace que la clave expire inmediatamente, por lo que no contamina la caché.

---

## Ejemplos de uso

### Query con caché automática (CacheBehavior)

```csharp
public sealed record GetCachedLearningHubQuery(int Id)
    : IQuery<LearningHubResponse>, ICacheable<LearningHubResponse>
{
    public string CacheKey => $"learninghub:{Id}";
    public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);
}

internal sealed class GetCachedLearningHubQueryHandler
    : IQueryHandler<GetCachedLearningHubQuery, LearningHubResponse>
{
    public ValueTask<Result<LearningHubResponse>> Handle(
        GetCachedLearningHubQuery request, CancellationToken cancellationToken)
    {
        // El handler solo se ejecuta si no hay caché.
        // CacheBehavior ya buscó y retornó el valor cacheado si existía.
        var hub = LearningHubStore.GetById(request.Id);

        return hub is null
            ? ValueTask.FromResult<Result<LearningHubResponse>>(
                Error.NotFound("learninghub.not_found", "..."))
            : ValueTask.FromResult<Result<LearningHubResponse>>(
                new LearningHubResponse(hub.Id, hub.Name, ...));
    }
}
```

### Uso directo de IHybridCacheService

```csharp
public class ProductService(IHybridCacheService cache)
{
    public async ValueTask<Product?> GetProductAsync(int id, CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            key: $"product:{id}",
            factory: async ct =>
            {
                // Solo se ejecuta si no está en caché
                return await dbContext.Products.FindAsync([id], ct);
            },
            expiration: TimeSpan.FromMinutes(10),
            cancellationToken: cancellationToken);
    }
}
```

### Invalidación manual de caché

```csharp
public async Task UpdateProductAsync(Product product, CancellationToken cancellationToken)
{
    dbContext.Products.Update(product);
    await dbContext.SaveChangesAsync(cancellationToken);

    // Invalidar caché: establecer con expiración cero para eliminar
    await cache.SetAsync<string>(
        key: $"product:{product.Id}",
        value: null!,
        expiration: TimeSpan.Zero,
        cancellationToken: cancellationToken);
}
```

### Verificar existencia en caché

```csharp
var lookup = await cache.GetAsync<Product>($"product:{id}", cancellationToken);
if (lookup.Found)
{
    // El producto está en caché
    var product = lookup.Value;
}
else
{
    // No está en caché, obtener de fuente y cachear
    var product = await dbContext.Products.FindAsync([id], cancellationToken);
    await cache.SetAsync($"product:{id}", product, TimeSpan.FromMinutes(10), cancellationToken);
}
```

### Uso en controlador

```csharp
[HttpGet("{id:int}/cached")]
public async Task<IResult> GetCachedById(int id, CancellationToken cancellationToken) =>
    (await dispatcher.Send(new GetCachedLearningHubQuery(id), cancellationToken)).ToOk();
// CacheBehavior intercepta automáticamente.
// Primera llamada: ejecuta el handler, cachea el resultado.
// Siguientes llamadas (dentro del TTL): retorna de caché sin ejecutar el handler.
```

---

## Testing

### Mock de IHybridCacheService

```csharp
var mockCache = new Mock<IHybridCacheService>();

mockCache.Setup(c => c.GetAsync<LearningHubResponse>(
        "learninghub:1", It.IsAny<CancellationToken>()))
    .ReturnsAsync(new CacheLookup<LearningHubResponse>(true, new LearningHubResponse(1, ...)));

var service = new ProductService(mockCache.Object);
var result = await service.GetProductAsync(1, CancellationToken.None);
```

### Test de CacheBehavior

```csharp
// Simular cache hit
var mockCache = new Mock<IHybridCacheService>();
mockCache.Setup(c => c.GetAsync<MyResponse>("key", It.IsAny<CancellationToken>()))
    .ReturnsAsync(new CacheLookup<MyResponse>(true, cachedValue));

// El handler no debería ejecutarse en cache hit
bool handlerCalled = false;

// El behavior retorna el valor cacheado directamente
```

### Test de CacheHealthCheck

```csharp
var mockCache = new Mock<IHybridCacheService>();
mockCache.Setup(c => c.GetAsync<string>("HealthCheckTestKey", It.IsAny<CancellationToken>()))
    .ReturnsAsync(new CacheLookup<string>(true, "HealthCheckTestValue"));

var check = new CacheHealthCheck(mockCache.Object);
var result = await check.CheckHealthAsync(new HealthCheckContext());

Assert.Equal(HealthStatus.Healthy, result.Status);
```
