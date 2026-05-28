# HttpClient Extensions y Configuration

## Que es

`HttpClientExtensions` es un conjunto de metodos de extension para `HttpClient` e `IHttpClientFactory` que proporcionan operaciones HTTP tipadas con mapeo automatico a `Result<T>`/`Error`. `HttpClientConfiguration` registra clientes nombrados con resiliencia (Retry, Circuit Breaker, Rate Limiting, Timeout) a partir de `HttpClientSettings[]`.

Cada cliente HTTP se configura declarativamente desde appsettings y se consume via `IHttpClientFactory` o las extensiones, que encapsulan errores de transporte y negocio en tipos `Result`.

**Paquete:** `Akay.To.Core`
**Extensiones:** `Akay.To.Core.Infrastructure.Extensions.HttpClientExtensions`
**Configuracion DI:** `Akay.To.Core.Infrastructure.DependencyInjection.HttpClientConfiguration`
**Settings:** `Akay.To.Core.Application.ApplicationSettings.HttpClientSettings`
**Validacion:** `Akay.To.Core.Application.ApplicationSettings.BaseApplicationSettingsValidator`

---

## Por que usarlo

- **Result<T> nativo del Core:** todos los metodos devuelven `Result<T?>` o `Result<string>`, alineado con el patron de resultados de `Akay.To.Core.Application.Results`.
- **Mapeo HTTP automatico:** los HTTP status codes se traducen automaticamente a `ErrorType` (Validation, Unauthorized, Forbidden, NotFound, Conflict, Timeout, Unavailable, Failure) con `Error.Code = "http.{statusCode}"`.
- **Errores de transporte capturados:** `TaskCanceledException` (timeout) y `HttpRequestException` (fallo de red) se convierten en `Error.Timeout` y `Error.Unavailable` respectivamente, sin usar excepciones no controladas.
- **Codigo DRY:** la logica comun de `try/catch` esta centralizada en `ExecuteWithTransportErrorHandling<T>`. Cada metodo publico solo define la operacion HTTP especifica.
- **Resiliencia declarativa:** se configura Retry, Circuit Breaker, Rate Limiting (ConcurrencyLimiter) y Timeout por intento/total desde appsettings.
- **Health checks por cliente:** se registra un health check por cada `HttpClientSettings` con `HealthEndpoint`, usando `IHttpClientFactory` (no `new HttpClient`). Distingue `Healthy`, `Degraded` (5xx/timeout) y `Unhealthy` (4xx/excepcion).
- **Validacion temprana:** las reglas de configuracion se validan via `BaseApplicationSettingsValidator` con FluentValidation, fallando en startup si la config es invalida.
- **Soporte de streaming:** `GetStreamAsync` permite consumir respuestas JSON como `IAsyncEnumerable<string>` con chunks UTF-8 de 1KB.
- **Endpoints relativos:** `GetJsonAsync(string endpoint)` soporta URIs relativas usando `BaseAddress` del cliente configurado.
- **Default headers seguros:** `Accept` y `User-Agent` se configuran con `ParseAdd` (validacion de formato). Headers personalizados se filtran (key y value no vacios).

---

## Arquitectura

### HttpClientExtensions (API publica)

```csharp
public static class HttpClientExtensions
{
    // Streaming
    public static async IAsyncEnumerable<string> GetStreamAsync<T>(
        this IHttpClientFactory httpClientFactory,
        string httpClientName,
        string endpoint,
        T body,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    // GET raw string
    public static Task<Result<string>> GetHttpResultStringAsync(
        this HttpClient httpClient,
        Uri uri,
        CancellationToken cancellationToken = default);

    // GET JSON (string endpoint, soporta relativo)
    public static Task<Result<T?>> GetJsonAsync<T>(
        this HttpClient httpClient,
        string endpoint,
        CancellationToken cancellationToken = default);

    // GET JSON (Uri absoluta)
    public static Task<Result<T?>> GetJsonAsync<T>(
        this HttpClient httpClient,
        Uri uri,
        CancellationToken cancellationToken = default);

    // POST JSON
    public static Task<Result<TResponse?>> PostJsonAsync<TResponse, TBody>(
        this HttpClient httpClient,
        string endpoint,
        TBody body,
        CancellationToken cancellationToken = default);

    // PUT JSON
    public static Task<Result<TResponse?>> PutJsonAsync<TResponse, TBody>(
        this HttpClient httpClient,
        string endpoint,
        TBody body,
        CancellationToken cancellationToken = default);
}
```

### Result<T> y Error

```csharp
namespace Akay.To.Core.Application.Results;

public readonly struct Result<TValue> : IApplicationResult
{
    public bool IsSuccess { get; }
    public bool IsFailure { get; }
    public TValue? Value { get; }
    public Error Error { get; }

    public static Result<TValue> Success(TValue? value);
    public static Result<TValue> Failure(Error error);
}

public readonly record struct Error
{
    public string Code { get; }
    public string Description { get; }
    public ErrorType Type { get; }
    public bool IsNone { get; }

    public static Error Validation(string code, string description);
    public static Error NotFound(string code, string description);
    public static Error Conflict(string code, string description);
    public static Error Unauthorized(string code, string description);
    public static Error Forbidden(string code, string description);
    public static Error Failure(string code, string description);
    public static Error Internal(string code, string description);
    public static Error Timeout(string code, string description);
    public static Error Unavailable(string code, string description);
    public static Error Cancelled(string code, string description);
}

public enum ErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    Failure = 6,
    Internal = 7,
    Timeout = 8,
    Unavailable = 9,
    Cancelled = 10
}
```

---

## Configuracion

### Registro en DI

```csharp
using Akay.To.Core.Infrastructure.DependencyInjection;

services.AddHttpClients(
    settings.BaseApplicationSettings.HttpClientSettings,
    settings.BaseApplicationSettings.Application?.Name,
    settings.BaseApplicationSettings.Application?.Version);
```

### Que registra

| Servicio | Lifetime |
|---|---|
| `HttpClient` nombrado (uno por entry en `HttpClientSettings[]`) | Scoped (default de `AddHttpClient`) |
| `DelegatingHandler` pipeline de resiliencia (Rate Limit, Retry, Timeout, Circuit Breaker) | Per-client |
| Header propagation (si `HeaderPropagation = true`) | Per-client |
| `HealthCheck` `{name}_httpclient` (si `HealthEndpoint` configurado) | Singleton |

Si `HttpClientSettings[]` es `null`, no se registra nada y el sistema opera sin clientes HTTP preconfigurados (fail-safe).

### HttpClientSettings

```csharp
public class HttpClientSettings
{
    public string? Name { get; set; }
    public string? BaseUrl { get; set; }
    public string? HealthEndpoint { get; set; }
    public string? Accept { get; set; }
    public string? UserAgent { get; set; }
    public Headers[]? Headers { get; set; }
    public bool HeaderPropagation { get; set; } = true;
    public HttpTimeout? Timeout { get; set; }
    public Retry? Retry { get; set; }
    public CircuitBreaker? CircuitBreaker { get; set; }
    public RateLimit? RateLimit { get; set; }
    public FailureConditions? FailureConditions { get; set; }
}

public class Headers
{
    public string? Key { get; set; }
    public string? Value { get; set; }
}

public class HttpTimeout
{
    public int? AttemptSeconds { get; set; } = 5;
    public int? TotalTimeoutSeconds { get; set; } = 20;
}

public class Retry
{
    public int? MaxAttempts { get; set; } = 3;
    public int? BaseDelayMs { get; set; } = 500;
    public string? BackoffType { get; set; } = "Exponential"; // Constant | Linear | Exponential
    public bool? UseJitter { get; set; } = true;
}

public class CircuitBreaker
{
    public int? SamplingDurationSeconds { get; set; } = 30;
    public double? FailureRatio { get; set; } = 0.5;
    public int? MinimumThroughput { get; set; } = 10;
    public int? BreakDurationSeconds { get; set; } = 30;
}

public class RateLimit
{
    public int? PermitLimit { get; set; } = 10;
    public int? QueueLimit { get; set; } = 20;
}

public class FailureConditions
{
    public int[]? RetryOnStatusCodes { get; set; }
    public int[]? CircuitBreakerOnStatusCodes { get; set; }
}
```

### Explicacion de cada parametro

Consulta la implementacion en `Akay.To.Core.Infrastructure.DependencyInjection.HttpClientConfiguration.AddHttpClients` y `Akay.To.Core.Application.ApplicationSettings.BaseApplicationSettings`.

| Parametro | Tipo | Default | Para que sirve |
|---|---|---|---|
| **Name** | `string` | *requerido* | Nombre unico del cliente registrado en `IHttpClientFactory`. Se usa para crear el cliente via `factory.CreateClient(Name)` y como nombre del health check (`{name}_httpclient`). |
| **BaseUrl** | `string` | *requerido* | URL base asignada a `HttpClient.BaseAddress`. Permite usar endpoints relativos en las extensiones. Debe ser URI absoluta (ej: `https://api.example.com/v1/`). |
| **HealthEndpoint** | `string` | `null` | Ruta relativa o absoluta para health check. Si se configura, registra un `HttpClientHealthCheck` que hace `GET` a este endpoint con el cliente nombrado. Resultado: `Healthy` (2xx), `Degraded` (5xx/timeout), `Unhealthy` (4xx/excepcion). |
| **Accept** | `string` | `application/json` | Valor del header HTTP `Accept`. Se aplica via `DefaultRequestHeaders.Accept.ParseAdd()`, que valida el formato MIME. |
| **UserAgent** | `string` | `{appName}/{version}` | Valor del header `User-Agent`. Si no se configura, se genera como `{Application.Name}/{Application.Version}` (fallback: `unknown-app/0.0.0`). |
| **Headers** | `Headers[]` | `null` | Array de headers adicionales agregados a `DefaultRequestHeaders.Add(key, value)`. Cada entry necesita `Key` y `Value` no vacios; los vacios se omiten silenciosamente. |
| **HeaderPropagation** | `bool` | `true` | Si es `true`, registra `AddHeaderPropagation()` en el pipeline. Propaga headers del request entrante (ej: `Authorization`, `CorrelationId`) a las llamadas salientes de este cliente. |
| **Timeout.AttemptSeconds** | `int?` | `5` | Timeout maximo por intento HTTP individual (segundos). Se agrega al pipeline como `pipeline.AddTimeout(...)`. Si se agota, el intento falla y puede disparar un retry. Si es `null`, no hay timeout por intento. |
| **Timeout.TotalTimeoutSeconds** | `int?` | `20` | Timeout maximo de la operacion completa (segundos), incluyendo todos los reintentos. Se agrega como `HttpTimeoutStrategyOptions` al final del pipeline. Si es `null`, no hay timeout total. |
| **Retry.MaxAttempts** | `int?` | `3` | Numero maximo de reintentos. `0` = sin reintentos. En la implementacion, el default real del pipeline es `0`, por lo que debe configurarse explicitamente para activar retries. |
| **Retry.BaseDelayMs** | `int?` | `500` | Tiempo base de espera entre reintentos (milisegundos). Con `Exponential`: delay = BaseDelayMs * 2^(n-1). Con `Linear`: delay = BaseDelayMs * n. Con `Constant`: delay = BaseDelayMs. |
| **Retry.BackoffType** | `string` | `Exponential` | Estrategia de backoff: `Constant` (siempre mismo delay), `Linear` (crece linealmente), `Exponential` (crece exponencialmente). Se convierte al enum `DelayBackoffType` de Polly. |
| **Retry.UseJitter** | `bool` | `true` | Si es `true`, agrega aleatoriedad al delay entre reintentos para evitar el efecto "thundering herd" cuando multiples clientes reintentan simultaneamente. |
| **CircuitBreaker.SamplingDurationSeconds** | `int?` | `30` | Ventana de tiempo (segundos) en la que se evalua la tasa de fallos para decidir si abrir el circuito. |
| **CircuitBreaker.FailureRatio** | `double?` | `0.5` | Proporcion de fallos (0.0 a 1.0) que debe superarse en `SamplingDuration` para abrir el circuito. `0.5` = 50% de fallos. |
| **CircuitBreaker.MinimumThroughput** | `int?` | `10` | Numero minimo de requests en la ventana de muestreo antes de que el circuito pueda abrirse. Evita aperturas prematuras con poco trafico. |
| **CircuitBreaker.BreakDurationSeconds** | `int?` | `30` | Tiempo (segundos) que el circuito permanece abierto antes de pasar a half-open y permitir un request de prueba. |
| **RateLimit.PermitLimit** | `int?` | `10` | Maximo de requests concurrentes permitidos. Se implementa con `ConcurrencyLimiter`. Requests que exceden este limite esperan en cola o son rechazados. |
| **RateLimit.QueueLimit** | `int?` | `0` | Maximo de requests en cola de espera. `0` = sin cola (requests excedentes se rechazan inmediatamente). Procesamiento FIFO (`OldestFirst`). |
| **FailureConditions.RetryOnStatusCodes** | `int[]` | `null` | Lista de HTTP status codes que disparan reintento. Si es `null`, el comportamiento por defecto reintenta en: `HttpRequestException`, `TimeoutException`, y status codes >= 500. |
| **FailureConditions.CircuitBreakerOnStatusCodes** | `int[]` | `null` | Lista de HTTP status codes que el Circuit Breaker cuenta como fallo. Si es `null`, el comportamiento por defecto cuenta: `HttpRequestException`, `TimeoutException`, y status codes >= 500. |

### appsettings.json

```json
{
  "HttpClientSettings": [
    {
      "Name": "billing",
      "BaseUrl": "https://billing.example.com/api/v3/",
      "HealthEndpoint": "/health",
      "Accept": "application/json",
      "UserAgent": "AkayTo/1.0",
      "Headers": [
        { "Key": "X-Api-Key", "Value": "sk-abc123" }
      ],
      "HeaderPropagation": true,
      "Timeout": {
        "AttemptSeconds": 5,
        "TotalTimeoutSeconds": 20
      },
      "Retry": {
        "MaxAttempts": 3,
        "BaseDelayMs": 500,
        "BackoffType": "Exponential",
        "UseJitter": true
      },
      "CircuitBreaker": {
        "SamplingDurationSeconds": 30,
        "FailureRatio": 0.5,
        "MinimumThroughput": 10,
        "BreakDurationSeconds": 30
      },
      "RateLimit": {
        "PermitLimit": 10,
        "QueueLimit": 20
      },
      "FailureConditions": {
        "RetryOnStatusCodes": [500, 502, 503, 504],
        "CircuitBreakerOnStatusCodes": [500, 502, 503]
      }
    }
  ]
}
```

### Validacion en startup

El validator `BaseApplicationSettingsValidator<T>` aplica las siguientes reglas a cada `HttpClientSettings` via `RuleForEach`:

| Regla | Campo | Condicion |
|---|---|---|
| `NotEmpty` | `Name` | Siempre |
| `NotEmpty` + URI absoluta | `BaseUrl` | Siempre |
| URI valida (rel/abs) | `HealthEndpoint` | Si no es nulo/vacio |
| `>= 0` | `Retry.MaxAttempts` | Si existe |
| `> 0` | `Retry.BaseDelayMs` | Si existe |
| `Constant`/`Linear`/`Exponential` | `Retry.BackoffType` | Si existe |
| `> 0` | `Timeout.AttemptSeconds` | Si existe |
| `> 0` | `Timeout.TotalTimeoutSeconds` | Si existe |
| `> 0` | `CircuitBreaker.SamplingDurationSeconds` | Si existe |
| `0.0 - 1.0` | `CircuitBreaker.FailureRatio` | Si existe |
| `> 0` | `CircuitBreaker.MinimumThroughput` | Si existe |
| `> 0` | `CircuitBreaker.BreakDurationSeconds` | Si existe |
| `> 0` | `RateLimit.PermitLimit` | Si existe |
| `>= 0` | `RateLimit.QueueLimit` | Si existe |
| `NotEmpty` | `Headers[].Key` | Si hay headers |
| `NotEmpty` | `Headers[].Value` | Si hay headers |

Si alguna regla falla, FluentValidation lanza excepcion en startup (fail-fast).

---

## Guia de la API

### Streaming

#### GetStreamAsync\<T\>

Consume un endpoint POST que devuelve un stream de respuesta JSON como chunks de texto UTF-8. Util cuando el servidor produce respuestas largas que deben procesarse incrementalmente sin cargar todo en memoria.

```csharp
public static async IAsyncEnumerable<string> GetStreamAsync<T>(
    this IHttpClientFactory httpClientFactory,
    string httpClientName,
    string endpoint,
    T body,
    [EnumeratorCancellation] CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `httpClientFactory` | `IHttpClientFactory` inyectado por DI. |
| `httpClientName` | Nombre del cliente configurado en `AddHttpClient`. |
| `endpoint` | Ruta relativa o absoluta del endpoint POST. |
| `body` | Payload serializado a JSON con `JsonOptions`. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `IAsyncEnumerable<string>` con chunks de texto de hasta 1024 caracteres UTF-8 decodificados desde el stream de respuesta. |
| **Excepciones** | `HttpRequestException` si el status code no es 2xx (`EnsureSuccessStatusCode`). `OperationCanceledException` si se cancela. |

```csharp
var factory = app.Services.GetRequiredService<IHttpClientFactory>();

var request = new { query = "buscar", maxResults = 50 };

await foreach (var chunk in factory.GetStreamAsync("ai-service", "v1/chat/stream", request, ct))
{
    Console.Write(chunk); // procesa incrementalmente
}
```

---

### GET

#### GetHttpResultStringAsync

Ejecuta `GET` a una URI absoluta y devuelve el body como `string` dentro de `Result<string>`.

```csharp
public static Task<Result<string>> GetHttpResultStringAsync(
    this HttpClient httpClient,
    Uri uri,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `httpClient` | Instancia de `HttpClient`. |
| `uri` | URI absoluta del recurso. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<string>.Success(body)` en 2xx. `Result<string>.Failure(Error)` con mapping HTTP en no-2xx. `Error.Timeout` si timeout de transporte. `Error.Unavailable` si `HttpRequestException`. |

```csharp
var client = factory.CreateClient("billing");
var result = await client.GetHttpResultStringAsync(new Uri("https://billing.example.com/status"), ct);

if (result.TryGetValue(out var body))
    Console.WriteLine(body);
else
    _logger.LogError("Billing status failed: {Error}", result.Error);
```

#### GetJsonAsync\<T\> (string)

Ejecuta `GET` usando un endpoint relativo (requiere `BaseAddress` configurado en el cliente) y deserializa la respuesta a `T`.

```csharp
public static Task<Result<T?>> GetJsonAsync<T>(
    this HttpClient httpClient,
    string endpoint,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `httpClient` | Instancia de `HttpClient` con `BaseAddress` configurado. |
| `endpoint` | Ruta relativa (ej: `"api/users"`) o absoluta. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<T?>.Success(valor)` en 2xx con JSON valido. `Result<T?>.Success(default)` si body vacio. `Result<T?>.Failure(Error.Internal)` si JSON invalido. `Result<T?>.Failure(Error)` mapeado del status code en no-2xx. |

#### GetJsonAsync\<T\> (Uri)

Ejecuta `GET` con una URI absoluta. Misma semantica que la sobrecarga con `string`.

```csharp
public static Task<Result<T?>> GetJsonAsync<T>(
    this HttpClient httpClient,
    Uri uri,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `uri` | URI absoluta del recurso. |

```csharp
public record InvoiceDto(string Id, decimal Total);

var client = factory.CreateClient("billing");

// Con endpoint relativo
var result = await client.GetJsonAsync<InvoiceDto>("invoices/INV-001", ct);

// Con URI absoluta
var result2 = await client.GetJsonAsync<InvoiceDto>(
    new Uri("https://billing.example.com/invoices/INV-001"), ct);

result.Match(
    onSuccess: invoice => Console.WriteLine($"Total: {invoice.Total}"),
    onFailure: error => _logger.LogError("Error HTTP: {Code} - {Desc}", error.Code, error.Description));
```

---

### POST

#### PostJsonAsync\<TResponse, TBody\>

Serializa `TBody` a JSON y ejecuta `POST`. Deserializa la respuesta a `TResponse`.

```csharp
public static Task<Result<TResponse?>> PostJsonAsync<TResponse, TBody>(
    this HttpClient httpClient,
    string endpoint,
    TBody body,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `httpClient` | Instancia de `HttpClient`. |
| `endpoint` | Ruta del endpoint. |
| `body` | Payload a serializar como JSON. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<TResponse?>` con la misma semantica de mapeo de errores que `GetJsonAsync`. |

```csharp
var client = factory.CreateClient("billing");

var newInvoice = new { customerId = "CUST-42", amount = 99.99m };
var result = await client.PostJsonAsync<InvoiceDto, object>("invoices", newInvoice, ct);

if (result.IsSuccess)
    Console.WriteLine($"Invoice created: {result.Value!.Id}");
```

---

### PUT

#### PutJsonAsync\<TResponse, TBody\>

Serializa `TBody` a JSON y ejecuta `PUT`. Deserializa la respuesta a `TResponse`.

```csharp
public static Task<Result<TResponse?>> PutJsonAsync<TResponse, TBody>(
    this HttpClient httpClient,
    string endpoint,
    TBody body,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `httpClient` | Instancia de `HttpClient`. |
| `endpoint` | Ruta del endpoint. |
| `body` | Payload a serializar como JSON. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<TResponse?>` con la misma semantica de mapeo de errores que `GetJsonAsync`. |

```csharp
var client = factory.CreateClient("billing");

var update = new { status = "paid" };
var result = await client.PutJsonAsync<InvoiceDto, object>("invoices/INV-001", update, ct);

result.Match(
    onSuccess: invoice => Console.WriteLine($"Updated: {invoice.Status}"),
    onFailure: error => _logger.LogError("PUT failed: {Code}", error.Code));
```

---

## Implementacion interna

### ExecuteWithTransportErrorHandling

Centraliza el manejo de excepciones de transporte. Todos los metodos publicos delegan en este helper.

```csharp
private static async Task<Result<T>> ExecuteWithTransportErrorHandling<T>(
    Func<CancellationToken, Task<Result<T>>> operation,
    CancellationToken cancellationToken)
{
    try
    {
        return await operation(cancellationToken);
    }
    catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
    {
        return Result<T>.Failure(Error.Timeout("http.timeout", "HTTP request timed out."));
    }
    catch (HttpRequestException ex)
    {
        return Result<T>.Failure(Error.Unavailable("http.request_failed", ex.Message));
    }
}
```

**Flujo:** ejecuta la operacion. Si `TaskCanceledException` y no fue cancelacion del caller, mapea a `Error.Timeout`. Si `HttpRequestException`, mapea a `Error.Unavailable`.

### ReturnJsonAsync

Procesa un `HttpResponseMessage`: extrae el body, valida status code, y deserializa JSON.

```csharp
private static async Task<Result<T?>> ReturnJsonAsync<T>(
    HttpResponseMessage response,
    CancellationToken cancellationToken);
```

**Flujo:**
1. Obtiene el string raw via `GetRawAsync`.
2. Si `raw.IsFailure` (status code no 2xx), propaga el `Error` mapeado.
3. Si `raw.Value` es nulo o whitespace, retorna `Success(default)`.
4. Deserializa con `JsonSerializer.Deserialize<T>(raw.Value, JsonOptions)`.
5. Si falla la deserializacion, retorna `Error.Internal("http.deserialize_error", ...)`.

### GetRawAsync

Lee el body como string y mapea el status code a `Result<string>`.

```csharp
private static async Task<Result<string>> GetRawAsync(
    HttpResponseMessage response,
    CancellationToken cancellationToken);
```

**Flujo:** `ReadAsStringAsync`. Si `!IsSuccessStatusCode`, llama a `MapHttpError`. Si es 2xx, retorna `Success(content)`.

### MapHttpError

Traduce un `HttpStatusCode` a `ErrorType` del dominio.

```csharp
private static Error MapHttpError(
    HttpStatusCode statusCode,
    string? reasonPhrase,
    string? content);
```

| Status Code | ErrorType | Codigo |
|---|---|---|
| 400, 422 | Validation | http.400 / http.422 |
| 401 | Unauthorized | http.401 |
| 403 | Forbidden | http.403 |
| 404 | NotFound | http.404 |
| 409 | Conflict | http.409 |
| 408 | Timeout | http.408 |
| 5xx | Unavailable | http.5xx |
| Otros 4xx | Failure | http.4xx |

### BuildErrorDescription

Construye la descripcion del error truncando el body a 1024 caracteres.

Formato: `{statusCode} {reasonPhrase}` o `{statusCode} {reasonPhrase} - {body}`.

### ParseBackoff

Convierte el string de configuracion de backoff al enum de Polly.

```csharp
private static DelayBackoffType ParseBackoff(string? type) =>
    type?.ToLowerInvariant() switch
    {
        "constant" => DelayBackoffType.Constant,
        "linear" => DelayBackoffType.Linear,
        _ => DelayBackoffType.Exponential
    };
```

### BuildDefaultUserAgent

Genera un User-Agent por defecto con formato `{appName}/{version}` si no se configura uno explicito. Usa `"unknown-app/0.0.0"` como fallback.

### HttpClientHealthCheck

```csharp
internal sealed class HttpClientHealthCheck(
    IHttpClientFactory httpClientFactory,
    string httpClientName,
    string healthEndpoint) : IHealthCheck;
```

**Flujo:**
1. Obtiene el `HttpClient` nombrado via `IHttpClientFactory.CreateClient(httpClientName)`.
2. Ejecuta `GET` al `healthEndpoint`.
3. Segun resultado:
   - 2xx: `HealthCheckResult.Healthy()`.
   - 5xx: `HealthCheckResult.Degraded(...)` con data `{client, endpoint, statusCode}`.
   - 4xx: `HealthCheckResult.Unhealthy(...)` con data `{client, endpoint, statusCode}`.
   - Timeout (`TaskCanceledException` sin cancelacion del caller): `HealthCheckResult.Degraded(...)` con exception y data.
   - Otra excepcion: `HealthCheckResult.Unhealthy(...)`.

---

## Resiliencia HTTP

El pipeline de resiliencia por cliente se construye con `Microsoft.Extensions.Http.Resilience` y `Polly`. El orden de ejecucion es:

1. **Rate Limit:** `ConcurrencyLimiter` con `PermitLimit` y `QueueLimit`. Limita requests concurrentes al mismo cliente. `QueueProcessingOrder.OldestFirst`.
2. **Retry:** reintenta en errores transitorios (`HttpRequestException`, `TimeoutException`, o 5xx por defecto; o los `FailureConditions.RetryOnStatusCodes` si se configuran). Usa Jitter por defecto.
3. **Timeout por intento:** `TimeSpan` basado en `Timeout.AttemptSeconds`. Timeout individual de cada intento HTTP.
4. **Circuit Breaker:** abre el circuito si `FailureRatio` de fallos en `SamplingDuration` supera el umbral, con `MinimumThroughput` minimo. Evalua `FailureConditions.CircuitBreakerOnStatusCodes` si se configuran, o 5xx por defecto.
5. **Timeout total:** `Timeout.TotalTimeoutSeconds`. Timeout global de la operacion completa (incluye todos los retries).

| Pipeline Step | Default | Configurable via |
|---|---|---|
| Rate Limit | PermitLimit=10, QueueLimit=0 | `RateLimit` |
| Retry | MaxAttempts=0, Delay=200ms, Exponential, Jitter=true | `Retry` |
| Timeout por intento | No aplica (null) | `Timeout.AttemptSeconds` |
| Circuit Breaker | 30s sampling, 0.5 ratio, 10 min throughput, 30s break | `CircuitBreaker` |
| Timeout total | No aplica (null) | `Timeout.TotalTimeoutSeconds` |

Si `RetryOnStatusCodes` es `null`, se usa el comportamiento por defecto: reintentar en `HttpRequestException`, `TimeoutException`, y status codes >= 500.

---

## Health Check

### Nombre y Tags

Cada health check se registra con nombre `{clientName.ToLowerInvariant()}_httpclient` y tag `httpclient`.

### Que verifica

- Realiza `GET` al `HealthEndpoint` configurado usando el `HttpClient` nombrado (con `BaseAddress` de la configuracion).
- No crea recursos ni modifica estado.
- El resultado depende del status code y tipo de excepcion.

### Endpoint de health

Los health checks de clientes HTTP se exponen en el endpoint estandar de health checks de ASP.NET Core (tipicamente `/health`), bajo la categoria `httpclient`.

---

## Consideraciones

### Overwrite / Idempotencia

`PostJsonAsync` y `PutJsonAsync` no incluyen logica de deteccion de duplicados ni idempotencia. Si el endpoint remoto no es idempotente, el retry del pipeline de resiliencia puede causar operaciones duplicadas. Configurar `Retry.MaxAttempts = 0` para operaciones no idempotentes.

### Validaciones

Todas las validaciones de configuracion (`Name`, `BaseUrl`, rangos de resiliencia) se ejecutan en startup via `BaseApplicationSettingsValidator`. Una configuracion invalida impide el arranque de la aplicacion.

### Endpoints relativos

`GetJsonAsync<T>(string)` usa `httpClient.GetAsync(endpoint, ...)`, que resuelve rutas relativas contra `BaseAddress`. Si `BaseAddress` no esta configurado, lanza `InvalidOperationException`. Usar la sobrecarga con `Uri` para URIs absolutas.

### Timeout vs cancelacion

La distincion entre timeout y cancelacion se basa en `TaskCanceledException when !cancellationToken.IsCancellationRequested`. Si el token fue cancelado por el caller, la excepcion se propaga sin ser capturada.

### Body en errores

`BuildErrorDescription` incluye el body de respuesta en el `Error.Description`, truncado a 1024 caracteres. Esto puede exponer informacion sensible del endpoint remoto en logs. Considerar sanitizar si el body contiene PII.

### Uso de memoria en GetRawAsync

`GetRawAsync` siempre lee el body completo via `ReadAsStringAsync`. Para respuestas grandes (ej: > 10 MB), esto puede causar presion de memoria. En esos casos usar `GetStreamAsync` para procesamiento incremental.

### Headers con valores vacios

Headers personalizados con `Key` o `Value` nulo/vacio/whitespace se omiten silenciosamente durante el registro del cliente. Esto evita excepciones en runtime pero puede ocultar errores de configuracion. La validacion en startup via `BaseApplicationSettingsValidator` los detecta.

### Singleton del health check builder

`services.AddHealthChecks()` se llama una unica vez al inicio del `foreach` y se reutiliza el builder para todos los health checks. Esto evita configuracion duplicada del pipeline de health checks.

---

## Testing

### Tests unitarios de extensiones HTTP

Los tests de `HttpClientExtensions` usan `DelegatingHandler` para simular respuestas HTTP sin servidor real.

Archivo: `Akay.To.Core.Tests\Infrastructure\HttpClientExtensionsTests.cs`

```csharp
// Helper para crear HttpClient con handler fake y BaseAddress por defecto
private static HttpClient CreateHttpClient(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
    Uri? baseAddress = null)
    => new(new DelegateHttpMessageHandler(handler))
    {
        BaseAddress = baseAddress ?? new Uri("https://test.local/")
    };

private sealed class DelegateHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => handler(request, cancellationToken);
}

private sealed record Payload(string? Name);
```

### Tests de mapeo de errores

```csharp
[Fact]
public async Task GetJsonAsync_Should_Map_NotFound_To_Error_NotFound()
{
    using var httpClient = CreateHttpClient((_, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            ReasonPhrase = "Not Found",
            Content = new StringContent("missing")
        }));

    var result = await httpClient.GetJsonAsync<object>("resource", TestContext.Current.CancellationToken);

    Assert.True(result.IsFailure);
    Assert.Equal(ErrorType.NotFound, result.Error.Type);
    Assert.Equal("http.404", result.Error.Code);
}

[Fact]
public async Task GetJsonAsync_Should_Map_Forbidden_To_Error_Forbidden()
{
    using var httpClient = CreateHttpClient((_, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            ReasonPhrase = "Forbidden"
        }));

    var result = await httpClient.GetJsonAsync<object>("resource", TestContext.Current.CancellationToken);

    Assert.True(result.IsFailure);
    Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    Assert.Equal("http.403", result.Error.Code);
}

[Fact]
public async Task GetJsonAsync_Should_Map_Timeout_To_Error_Timeout()
{
    using var httpClient = CreateHttpClient((_, _) =>
        throw new TaskCanceledException("timeout"));

    var result = await httpClient.GetJsonAsync<object>("resource", TestContext.Current.CancellationToken);

    Assert.True(result.IsFailure);
    Assert.Equal(ErrorType.Timeout, result.Error.Type);
    Assert.Equal("http.timeout", result.Error.Code);
}

[Fact]
public async Task GetJsonAsync_Should_Map_Invalid_Json_To_Error_Internal()
{
    using var httpClient = CreateHttpClient((_, _) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{ invalid-json")
        }));

    var result = await httpClient.GetJsonAsync<Payload>("resource", TestContext.Current.CancellationToken);

    Assert.True(result.IsFailure);
    Assert.Equal(ErrorType.Internal, result.Error.Type);
    Assert.Equal("http.deserialize_error", result.Error.Code);
}

[Fact]
public async Task GetJsonAsync_Should_Use_Relative_Endpoint_With_BaseAddress()
{
    using var httpClient = CreateHttpClient((request, _) =>
    {
        Assert.Equal(new Uri("https://api.example.com/v1/resource"), request.RequestUri);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"name\":\"ok\"}")
        });
    }, new Uri("https://api.example.com/"));

    var result = await httpClient.GetJsonAsync<Payload>("v1/resource", TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    Assert.Equal("ok", result.Value?.Name);
}
```

### Tests de validacion de settings

Para probar `BaseApplicationSettingsValidator`, crear una instancia del validator y ejecutar `Validate()` con diferentes configuraciones de `HttpClientSettings`:

```csharp
var validator = new BaseApplicationSettingsValidator<BaseApplicationSettings>();

// Caso valido
var settings = new BaseApplicationSettings
{
    AllowedHosts = "*",
    HttpClientSettings =
    [
        new HttpClientSettings
        {
            Name = "test",
            BaseUrl = "https://api.example.com/"
        }
    ]
};
var result = validator.Validate(settings);
Assert.True(result.IsValid);

// Caso invalido: Name vacio
settings.HttpClientSettings[0].Name = "";
result = validator.Validate(settings);
Assert.False(result.IsValid);
Assert.Contains(result.Errors, e => e.PropertyName.Contains("Name"));

// Caso invalido: BaseUrl no absoluta
settings.HttpClientSettings[0].Name = "test";
settings.HttpClientSettings[0].BaseUrl = "api/relative";
result = validator.Validate(settings);
Assert.False(result.IsValid);
Assert.Contains(result.Errors, e => e.PropertyName.Contains("BaseUrl"));
```
