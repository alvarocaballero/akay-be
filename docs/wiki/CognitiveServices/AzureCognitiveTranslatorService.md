# Azure Cognitive Translator Service

## Que es

`ICognitiveTranslatorService` abstrae la API REST de Azure Translator v3 para traduccion de texto. Soporta deteccion automatica de idioma de origen, traduccion a multiples idiomas destino simultaneamente, reintentos con backoff y timeout configurable, sin exponer dependencias HTTP ni del SDK de Azure en la capa de aplicacion.

**Paquete:** `Akay.To.Azure`
**Interfaz:** `Akay.To.Core.Application.Abstractions.CognitiveServices.ICognitiveTranslatorService`
**Implementacion:** `Akay.To.Azure.Infrastructure.Services.AzureCognitiveTranslatorService`
**Registro DI:** `Akay.To.Azure.Infrastructure.DependencyInjection.AzureCognitiveTranslatorConfiguration`

---

## Por que usarlo

- **Deteccion automatica de idioma:** si `fromLanguage` es `null` o vacio, el servicio detecta el idioma origen y lo incluye en `TranslationResult.DetectedLanguage`.
- **Traduccion multiple:** una sola llamada traduce a varios idiomas destino simultaneamente, reduciendo llamadas a la API.
- **Reintentos con backoff:** reintentos automaticos en fallos HTTP transitorios (5xx), timeouts, y `HttpRequestException`, con backoff configurable.
- **Timeout configurable:** `TimeoutSeconds` de `TranslatorSettings` cancela la operacion si la API no responde a tiempo.
- **Abstraccion desacoplada:** la capa de aplicacion no referencia `HttpClient` ni `Azure.AI.Translation`. Los tipos de transporte no se filtran al contrato.
- **Validacion temprana:** texto vacio e idiomas destino vacios se rechazan con `Error.Validation` antes de llamar a la API.
- **Parseo robusto:** la respuesta JSON se parsea con `JsonDocument` manejando arrays vacios, campos faltantes, y traducciones nulas.
- **Health check sin traduccion real:** verifica conectividad con la API usando un texto de prueba minimo ("ping"), sin depender de traducciones validas.

---

## Arquitectura

### ICognitiveTranslatorService

```csharp
public interface ICognitiveTranslatorService
{
    Task<Result<TranslationResult>> TranslateTextAsync(
        string text,
        string? fromLanguage,
        IReadOnlyCollection<string> toLanguages,
        CancellationToken cancellationToken = default);
}
```

### Tipos auxiliares

```csharp
public sealed record TranslationResult(
    string DetectedLanguage,
    IReadOnlyCollection<TranslationItem> Translations);

public sealed record TranslationItem(
    string Language,
    string TranslatedText);
```

---

## Configuracion

### Registro en DI

```csharp
using Akay.To.Azure.Infrastructure.DependencyInjection;

services.AddAzureCognitiveTranslatorServices();
```

### Que registra

| Servicio | Lifetime |
|---|---|
| `ICognitiveTranslatorService` | Transient |
| Health check `azure_translator` (tag: `translator`) | Se anade al pipeline |

### TranslatorSettings

```csharp
public class TranslatorSettings
{
    public string? BaseUrl { get; set; }
    public string? Key { get; set; }
    public string? Region { get; set; }
    public string? DefaultLanguage { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public int RetryMaxAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 300;
    public string BackoffType { get; set; } = "Exponential";
}
```

`TranslatorSettings` se ubica dentro de `BaseApplicationSettings.TranslatorSettings`.

### appsettings.json

```json
{
  "TranslatorSettings": {
    "BaseUrl": "https://api.cognitive.microsofttranslator.com/",
    "Key": "<azure-translator-key>",
    "Region": "westeurope",
    "DefaultLanguage": "es",
    "TimeoutSeconds": 15,
    "RetryMaxAttempts": 3,
    "RetryBaseDelayMs": 300,
    "BackoffType": "Exponential"
  }
}
```

Si `TranslatorSettings` es `null`, `TranslateTextAsync` devuelve `Error.Validation("translator.settings.missing")`.

---

## Guia de la API

### Traduccion

#### TranslateTextAsync

Traduce un texto a uno o varios idiomas de destino.

```csharp
Task<Result<TranslationResult>> TranslateTextAsync(
    string text,
    string? fromLanguage,
    IReadOnlyCollection<string> toLanguages,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `text` | Texto a traducir. No puede ser nulo o whitespace. Devuelve `Error.Validation("translator.text.empty")`. |
| `fromLanguage` | Codigo de idioma de origen (ej. `"en"`, `"es"`). Si es `null` o vacio, Azure detecta el idioma automaticamente. |
| `toLanguages` | Coleccion de codigos de idioma destino (ej. `["es", "fr", "de"]`). Debe contener al menos un idioma. Los duplicados se eliminan automaticamente. Devuelve `Error.Validation("translator.languages.empty")` si esta vacia. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<TranslationResult>` con el idioma detectado y la lista de traducciones. |

**Posibles errores:**
- `Error.Validation("translator.text.empty")` -- texto vacio.
- `Error.Validation("translator.languages.empty")` -- sin idiomas destino.
- `Error.Validation("translator.settings.missing")` -- `TranslatorSettings` es `null`.
- `Error.Cancelled("translator.cancelled")` -- cancelacion por token.
- `Error.Timeout("translator.timeout")` -- timeout agotado.
- `Error.Unavailable("translator.http.{status}")` -- error HTTP de la API.
- `Error.Unavailable("translator.request_failed")` -- fallo de red.
- `Error.Unavailable("translator.retry_exhausted")` -- reintentos agotados.
- `Error.Internal("translator.response.empty")` -- respuesta sin items.
- `Error.Internal("translator.detected_language.empty")` -- idioma detectado ausente.
- `Error.Internal("translator.translations.empty")` -- sin traducciones en la respuesta.
- `Error.Internal("translator.response.parse_error")` -- JSON invalido.
- `Error.Internal("translator.unhandled")` -- excepcion inesperada.

```csharp
var translator = serviceProvider.GetRequiredService<ICognitiveTranslatorService>();

var result = await translator.TranslateTextAsync(
    "Hello world",
    fromLanguage: null,  // auto-deteccion
    toLanguages: ["es", "fr", "de"],
    cancellationToken: ct);

if (result.IsSuccess)
{
    Console.WriteLine($"Idioma: {result.Value.DetectedLanguage}"); // "en"
    foreach (var t in result.Value.Translations)
        Console.WriteLine($"  {t.Language}: {t.TranslatedText}");
    //   es: Hola mundo
    //   fr: Bonjour le monde
    //   de: Hallo Welt
}
```

### TranslationResult

```csharp
public sealed record TranslationResult(
    string DetectedLanguage,
    IReadOnlyCollection<TranslationItem> Translations);
```

| Propiedad | Descripcion |
|---|---|
| `DetectedLanguage` | Codigo del idioma detectado (ej. `"en"`). Si se especifico `fromLanguage`, coincide con ese valor. |
| `Translations` | Coleccion de `TranslationItem`, uno por cada idioma destino solicitado. |

### TranslationItem

```csharp
public sealed record TranslationItem(
    string Language,
    string TranslatedText);
```

| Propiedad | Descripcion |
|---|---|
| `Language` | Codigo del idioma destino (ej. `"es"`). |
| `TranslatedText` | Texto traducido al idioma destino. |

---

## Implementacion interna

### Constructor de AzureCognitiveTranslatorService

```csharp
internal class AzureCognitiveTranslatorService(
    IOptions<BaseApplicationSettings> settings,
    IHttpClientFactory httpClientFactory) : ICognitiveTranslatorService
```

El servicio es **stateless**: no almacena configuracion ni clientes entre llamadas. Cada invocacion de `TranslateTextAsync` crea un `HttpClient` nuevo via `IHttpClientFactory` para garantizar aislamiento.

### Metodos privados clave

| Metodo | Funcion |
|---|---|
| `BuildEndpoint(string?, IReadOnlyCollection<string>)` | Construye la query string de la API: `api-version=3.0`, `from={lang}` opcional, `to={lang}` por cada destino (con `Uri.EscapeDataString`). |
| `ParseTranslationResponse(string, string?)` | Parsea la respuesta JSON de Azure Translator v3, extrayendo `detectedLanguage` y cada `translations[].to` / `translations[].text`. |
| `DelayForRetryAsync(TranslatorSettings, int, CancellationToken)` | Calcula el retraso de reintento segun `BackoffType` (constant/linear/exponential). |

### Flujo de TranslateTextAsync

```
TranslateTextAsync(text, fromLanguage, toLanguages, ct)
  |
  +--> Validar text no vacio -> Error.Validation
  +--> Validar toLanguages.Count > 0 -> Error.Validation
  +--> Validar TranslatorSettings no null -> Error.Validation
  |
  +--> timeoutCts = CancelAfter(TimeoutSeconds)
  +--> endpoint = BuildEndpoint(fromLanguage, toLanguages)
  +--> body = [{ Text = text }]
  |
  +--> for attempt = 1 .. RetryMaxAttempts:
         |
         +--> Crear HttpClient desde IHttpClientFactory
         +--> Set BaseAddress = BaseUrl
         +--> Headers: Ocp-Apim-Subscription-Key, Ocp-Apim-Subscription-Region
         |
         +--> client.PostAsJsonAsync(endpoint, body, timeoutCts.Token)
         |
         +--> Si !IsSuccessStatusCode:
         |      - rawError = response.Content.ReadAsStringAsync()
         |      - error = Error.Unavailable("translator.http.{status}")
         |      - Si attempt == RetryMaxAttempts -> return error
         |      - Sino: DelayForRetryAsync, continuar loop
         |
         +--> jsonResponse = response.Content.ReadAsStringAsync()
         +--> return ParseTranslationResponse(jsonResponse, fromLanguage)
         |
         +--> catch OperationCanceledException when ct.IsCancellationRequested:
         |      return Error.Cancelled
         |
         +--> catch OperationCanceledException when !ct.IsCancellationRequested:
         |      (timeout) -> si ultimo intento: Error.Timeout, sino reintentar
         |
         +--> catch HttpRequestException:
         |      -> si ultimo intento: Error.Unavailable, sino reintentar
         |
         +--> catch Exception:
                -> Error.Internal (no reintentable)
  |
  +--> return Error.Unavailable("translator.retry_exhausted")
```

### ParseTranslationResponse

```csharp
private static Result<TranslationResult> ParseTranslationResponse(
    string jsonResponse, string? fromLanguage)
{
    using var doc = JsonDocument.Parse(jsonResponse);
    // Valida que sea un array no vacio
    var root = doc.RootElement[0];

    // Usa fromLanguage si se especifico; sino extrae detectedLanguage
    var detectedLanguage = fromLanguage
        ?? root.GetProperty("detectedLanguage").GetProperty("language").GetString();

    // Itera translations[] extrayendo Language y TranslatedText
    // Omite items con lang o texto vacio

    return new TranslationResult(detectedLanguage, translations);
}
```

El parseo maneja:
- Array raiz vacio -> `Error.Internal("translator.response.empty")`
- `detectedLanguage` faltante cuando `fromLanguage` no se especifico -> `Error.Internal("translator.detected_language.empty")`
- `translations[]` sin items validos -> `Error.Internal("translator.translations.empty")`
- JSON malformado -> `Error.Internal("translator.response.parse_error")`

### BuildEndpoint

Construye la ruta de la API REST v3 con los parametros de idioma:

```
/translate?api-version=3.0&from=en&to=es&to=fr&to=de
```

- `from` solo se incluye si `fromLanguage` no es vacio.
- Los idiomas destino se escapan con `Uri.EscapeDataString`.
- Los duplicados en `toLanguages` se eliminan con `Distinct(StringComparer.OrdinalIgnoreCase)`.

### Health check

```csharp
internal class AzureTranslatorHealthCheck(IOptions<BaseApplicationSettings> settings)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        if (settings.Value.TranslatorSettings?.BaseUrl is null)
            return HealthCheckResult.Unhealthy(
                "Error en Azure Translator Service Configuration.");

        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(settings.Value.TranslatorSettings!.BaseUrl!)
            };
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key",
                settings.Value.TranslatorSettings?.Key);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Region",
                settings.Value.TranslatorSettings?.Region);

            using var content = new StringContent(
                JsonSerializer.Serialize(new[] { new { Text = "ping" } }),
                Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                "translate?api-version=3.0&from=en&to=es", content, ct);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy(
                    "Azure Translator Service esta disponible.")
                : HealthCheckResult.Unhealthy(
                    "Error en Azure Translator Service.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Error en Azure Translator Service.", ex);
        }
    }
}
```

- Usa `HttpClient` directo (no `IHttpClientFactory`) con el endpoint de traduccion.
- Traduce "ping" de ingles a espanol: una operacion real pero minima.
- **No reintenta ni aplica timeout propio** en el health check.
- Si `BaseUrl` es `null`, falla inmediatamente.

---

## Modelo de reintentos y timeout

`TranslateTextAsync` implementa un bucle de reintentos con las siguientes reglas:

| Condicion | Reintentable? | Error final si se agotan |
|---|---|---|
| HTTP status != 2xx | Si | `Error.Unavailable("translator.http.{status}")` |
| `OperationCanceledException` con `!ct.IsCancellationRequested` (timeout) | Si | `Error.Timeout("translator.timeout")` |
| `HttpRequestException` (fallo de red) | Si | `Error.Unavailable("translator.request_failed")` |
| `OperationCanceledException` con `ct.IsCancellationRequested` | No | `Error.Cancelled("translator.cancelled")` |
| `Exception` generica | No | `Error.Internal("translator.unhandled")` |

### Configuracion de backoff

| Parametro | Default | Descripcion |
|---|---|---|
| `RetryMaxAttempts` | 3 | Intentos totales |
| `RetryBaseDelayMs` | 300 | Retraso base |
| `BackoffType` | Exponential | `constant`, `linear` o `exponential` |

### Timeout

El timeout se aplica **por intento**, no global. Se usa `CancellationTokenSource.CancelAfter(TimeoutSeconds)` vinculado al token de cancelacion del caller. Si un intento excede `TimeoutSeconds`, se captura `OperationCanceledException` (sin `ct.IsCancellationRequested`) y se reintenta si quedan intentos.

---

## Health Check

El health check `azure_translator` (tag: `translator`) se registra automaticamente al llamar a `AddAzureCognitiveTranslatorServices()`.

- Realiza una peticion POST a `translate?api-version=3.0&from=en&to=es` con `[{"Text":"ping"}]`.
- **No requiere traduccion valida**, solo que la API responda 2xx.
- Si `BaseUrl` es `null`, falla inmediatamente.

```csharp
// Se registra automaticamente:
services.AddAzureCognitiveTranslatorServices();

// GET /healthz -> "azure_translator": Healthy/Unhealthy
```

---

## Consideraciones

### Auto-deteccion de idioma

Cuando `fromLanguage` es `null` o vacio, el servicio depende de Azure para detectar el idioma. La respuesta incluye `detectedLanguage` con el codigo ISO. Si Azure no puede detectarlo, `ParseTranslationResponse` devuelve `Error.Internal("translator.detected_language.empty")`.

### Limpieza de headers

Cada intento crea un `HttpClient` nuevo y elimina los headers `Ocp-Apim-Subscription-Key` y `Ocp-Apim-Subscription-Region` antes de anadirlos. Esto evita duplicados en reintentos (aunque el cliente es nuevo, es una defensa adicional).

### Duplicados en idiomas destino

`BuildEndpoint` aplica `Distinct(StringComparer.OrdinalIgnoreCase)` sobre `toLanguages`. Si pasas `["es", "ES", "Es"]`, solo se incluye uno.

### Sin cache

No existe cache de traducciones en esta implementacion. Cada llamada va a la API de Azure. Para cache, usa `IHybridCacheService` o `IBlobStorageService` en tu capa de aplicacion con la clave de cache adecuada.

### Sin traduccion de documentos

Esta abstraccion solo cubre traduccion de texto (API v3 `/translate`). No incluye traduccion de documentos (`/batches`).

---

## Testing

### Mock de ICognitiveTranslatorService

```csharp
var mockTranslator = new Mock<ICognitiveTranslatorService>();

// Exito con auto-deteccion
mockTranslator.Setup(t => t.TranslateTextAsync(
        "Hello", null, It.IsAny<IReadOnlyCollection<string>>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<TranslationResult>.Success(
        new TranslationResult("en", [
            new TranslationItem("es", "Hola"),
            new TranslationItem("fr", "Bonjour")
        ])));

// Exito con idioma origen especificado
mockTranslator.Setup(t => t.TranslateTextAsync(
        "Hola", "es", It.Is<IReadOnlyCollection<string>>(
            c => c.Contains("en")),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<TranslationResult>.Success(
        new TranslationResult("es", [
            new TranslationItem("en", "Hello")
        ])));

// Fallo de validacion
mockTranslator.Setup(t => t.TranslateTextAsync(
        "", null, It.IsAny<IReadOnlyCollection<string>>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<TranslationResult>.Failure(
        Error.Validation("translator.text.empty", "Text is required.")));

// Fallo de servicio
mockTranslator.Setup(t => t.TranslateTextAsync(
        It.IsAny<string>(), It.IsAny<string?>(),
        It.IsAny<IReadOnlyCollection<string>>(),
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<TranslationResult>.Failure(
        Error.Unavailable("translator.retry_exhausted", "fallo")));
```

---

## Ejemplos de uso

### Traduccion simple con auto-deteccion

```csharp
app.MapPost("/api/translate", async (
    TranslateRequest request,
    ICognitiveTranslatorService translator,
    CancellationToken ct) =>
{
    var result = await translator.TranslateTextAsync(
        request.Text,
        fromLanguage: null,
        toLanguages: request.ToLanguages,
        cancellationToken: ct);

    if (result.IsFailure)
        return Results.BadRequest(new { error = result.Error.Message });

    return Results.Ok(new
    {
        detected = result.Value.DetectedLanguage,
        translations = result.Value.Translations.Select(t => new
        {
            language = t.Language,
            text = t.TranslatedText
        })
    });
});

public record TranslateRequest(string Text, string[] ToLanguages);
```

### Traduccion con idioma origen conocido

```csharp
var result = await translator.TranslateTextAsync(
    "El clima es agradable hoy",
    fromLanguage: "es",
    toLanguages: ["en", "pt", "it"],
    cancellationToken: ct);

if (result.IsSuccess)
{
    foreach (var t in result.Value.Translations)
        Console.WriteLine($"{t.Language}: {t.TranslatedText}");
    // en: The weather is nice today
    // pt: O clima esta agradavel hoje
    // it: Il clima e piacevole oggi
}
```

### Manejo de errores granular

```csharp
var result = await translator.TranslateTextAsync(text, null, ["es"], ct);

if (result.IsSuccess)
    return result.Value.Translations.First().TranslatedText;

return result.Error.Code switch
{
    "translator.text.empty" => "Proporciona un texto para traducir",
    "translator.languages.empty" => "Especifica al menos un idioma destino",
    "translator.cancelled" => "Operacion cancelada",
    "translator.timeout" => "El servicio tardo demasiado, intenta de nuevo",
    "translator.retry_exhausted" => "Servicio no disponible, reintenta mas tarde",
    _ => "Error inesperado"
};
```
