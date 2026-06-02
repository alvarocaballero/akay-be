# Azure Cognitive Speech Service

## Que es

`ICognitiveSpeechService` abstrae las operaciones de voz (texto a voz y voz a texto) de Azure Cognitive Services Speech SDK. Proporciona sintesis de voz con streaming, cache en Blob Storage, y reconocimiento de voz con reintentos, sin exponer dependencias del SDK de Azure en la capa de aplicacion.

Cada operacion devuelve `Result<T>` para propagar errores de forma homogenea.

**Paquete:** `Akay.To.Azure`
**Interfaz:** `Akay.To.Core.Application.Abstractions.CognitiveServices.ICognitiveSpeechService`
**Implementacion:** `Akay.To.Azure.Infrastructure.Services.AzureCognitiveSpeechService`
**Registro DI:** `Akay.To.Azure.Infrastructure.DependencyInjection.AzureCognitiveSpeechConfiguration`

---

## Por que usarlo

- **Streaming nativo:** la sintesis de voz transmite fragmentos de audio via `IAsyncEnumerable<byte[]>` sin esperar a que el audio completo este generado.
- **Cache automatica en Blob Storage:** `TextToSpeechCacheableStreamAsync` almacena y recupera audios cacheados por hash de parametros de voz, evitando sintesis repetidas.
- **Reintentos con backoff:** `SpeechToTextAsync` reintenta con backoff configurable (constant, linear, exponential) cuando el reconocimiento falla.
- **Abstraccion desacoplada:** la capa de aplicacion no referencia `Microsoft.CognitiveServices.Speech`. Los tipos de Azure no se filtran al contrato.
- **Timeout configurable:** tanto sintesis como reconocimiento respetan `TimeoutSeconds` de `SpeechSettings`, cancelando operaciones bloqueadas.
- **De-duplicacion de texto reconocido:** `SpeechToTextStreamAsync` filtra fragmentos duplicados consecutivos evitando repeticiones.
- **Validacion temprana:** texto vacio y stream nulo se validan al inicio, con errores `Validation` antes de llamar al SDK.
- **Health check sin side effects:** verifica conectividad con la API de Speech via HTTP GET al endpoint de estado, sin crear recursos.
- **Configuracion fail-safe:** si `SpeechSettings` es `null`, el constructor usa valores por defecto sin lanzar excepcion.

---

## Arquitectura

### ICognitiveSpeechService

```csharp
public interface ICognitiveSpeechService
{
    IAsyncEnumerable<Result<byte[]>> TextToSpeechStreamAsync(string text,
                                                             CancellationToken cancellationToken = default);

    IAsyncEnumerable<Result<byte[]>> TextToSpeechCacheableStreamAsync(string? text,
                                                                      string blobStorageName,
                                                                      string audioName,
                                                                      CancellationToken cancellationToken = default);

    Task<Result<string>> SpeechToTextAsync(Stream audioStream,
                                           CancellationToken cancellationToken = default);

    IAsyncEnumerable<Result<string>> SpeechToTextStreamAsync(Stream audioStream,
                                                             CancellationToken cancellationToken = default);
}
```

---

## Configuracion

### Registro en DI

```csharp
using Akay.To.Azure.Infrastructure.DependencyInjection;

services.AddAzureCognitiveSpeechServices();
```

### Que registra

| Servicio | Lifetime |
|---|---|
| `ICognitiveSpeechService` | Transient |
| Health check `azure_speech` (tag: `speech`) | Se anade al pipeline |

### SpeechSettings

```csharp
public class SpeechSettings
{
    public string? Key { get; set; }
    public string? Region { get; set; }
    public string? Language { get; set; }
    public string? OutputFormat { get; set; }
    public string? VoiceName { get; set; }
    public uint? SampleRate { get; set; } = 16000;
    public byte? BitsPerSample { get; set; } = 16;
    public byte? Channels { get; set; } = 1;
    public int TimeoutSeconds { get; set; } = 30;
    public int RetryMaxAttempts { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 300;
    public string BackoffType { get; set; } = "Exponential";
}
```

`SpeechSettings` se ubica dentro de `BaseApplicationSettings.SpeechSettings`.

### appsettings.json

```json
{
  "SpeechSettings": {
    "Key": "<azure-speech-key>",
    "Region": "westeurope",
    "Language": "es-ES",
    "VoiceName": "es-ES-AlvaroNeural",
    "OutputFormat": "Riff24Khz16BitMonoPcm",
    "SampleRate": 24000,
    "BitsPerSample": 16,
    "Channels": 1,
    "TimeoutSeconds": 30,
    "RetryMaxAttempts": 3,
    "RetryBaseDelayMs": 300,
    "BackoffType": "Exponential"
  }
}
```

Si `SpeechSettings` es `null`, el constructor crea una instancia con valores por defecto y el servicio opera sin lanzar excepcion (aunque las llamadas a Azure fallaran por falta de Key/Region).

---

## Guia de la API

### Texto a Voz

#### TextToSpeechStreamAsync

Sintetiza texto a voz y transmite los fragmentos de audio WAV/PCM a medida que se generan.

```csharp
IAsyncEnumerable<Result<byte[]>> TextToSpeechStreamAsync(
    string text,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `text` | Texto a sintetizar. No puede ser nulo o whitespace. Devuelve `Error.Validation` si esta vacio. |
| `cancellationToken` | Token de cancelacion. Cancela la sintesis en curso y emite `Error.Cancelled`. |
| **Retorna** | `IAsyncEnumerable<Result<byte[]>>` -- cada elemento es un fragmento WAV/PCM. La secuencia termina al completar la sintesis o al producirse un error. |

**Posibles errores emitidos en la secuencia:**
- `Error.Validation("speech.text.empty")` -- texto vacio.
- `Error.Timeout("speech.timeout")` -- se agoto `TimeoutSeconds` sin completar sintesis.
- `Error.Cancelled("speech.cancelled")` -- cancelacion solicitada.
- `Error.Unavailable("speech.synthesis.failed")` -- el servicio de Azure reporto fallo.
- `Error.Internal("speech.synthesis.unhandled")` -- excepcion no esperada.

```csharp
var speech = serviceProvider.GetRequiredService<ICognitiveSpeechService>();

await foreach (var chunk in speech.TextToSpeechStreamAsync("Hola mundo", ct))
{
    if (chunk.IsFailure)
    {
        _logger.LogError("Sintesis fallida: {Error}", chunk.Error);
        break;
    }

    await outputStream.WriteAsync(chunk.Value!, ct);
}
```

#### TextToSpeechCacheableStreamAsync

Sintetiza texto a voz con cache en Blob Storage. Si el audio existe en cache, se sirve desde alli sin volver a sintetizar.

```csharp
IAsyncEnumerable<Result<byte[]>> TextToSpeechCacheableStreamAsync(
    string? text,
    string blobStorageName,
    string audioName,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `text` | Texto a sintetizar. No puede ser nulo o whitespace. |
| `blobStorageName` | Nombre del contenedor de Blob Storage donde se almacenara la cache. Se crea automaticamente si no existe (`forceCreateContainer: true`). |
| `audioName` | Nombre base del archivo de cache. Se combina con un hash SHA256 de los parametros de voz para generar el nombre final. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `IAsyncEnumerable<Result<byte[]>>` -- fragmentos de audio. Si existe en cache, se transmiten desde Blob Storage. Si no, se sintetizan y se guardan. |

**Flujo interno:**
1. Valida que `text` no este vacio.
2. Crea el blob storage service para el contenedor indicado.
3. Genera el nombre del archivo de cache con `BuildCacheFileName` (hash de `audioName + VoiceName + Language + OutputFormat + SampleRate + BitsPerSample + Channels`).
4. Si el blob existe, streamea desde Blob Storage.
5. Si no existe, sintetiza con `TextToSpeechStreamAsync`, acumula en `MemoryStream`, streamea en tiempo real, y al finalizar guarda en Blob Storage con escritura atomica (upload a temp, upload final con `overwrite: false`, delete temp).

```csharp
var speech = serviceProvider.GetRequiredService<ICognitiveSpeechService>();

await foreach (var chunk in speech.TextToSpeechCacheableStreamAsync(
    "Bienvenido al sistema", "audio-cache", "welcome-message", ct))
{
    if (chunk.IsFailure)
    {
        _logger.LogError("Error: {Error}", chunk.Error);
        break;
    }
    await response.Body.WriteAsync(chunk.Value!, ct);
}
```

---

### Voz a Texto

#### SpeechToTextAsync

Convierte un stream de audio WAV/PCM a texto completo, con reintentos automaticos.

```csharp
Task<Result<string>> SpeechToTextAsync(
    Stream audioStream,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `audioStream` | Stream de audio en formato WAV/PCM. Debe ser seekable para reintentos. Si no lo es, solo se permite un intento. Devuelve `Error.Validation` si es `null`. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<string>` con el texto completo reconocido. Los fragmentos se unen con espacio. |

**Posibles errores:**
- `Error.Validation("speech.audio.null")` -- stream nulo.
- `Error.Validation("speech.audio.not_seekable")` -- stream no seekable en reintento.
- `Error.Cancelled("speech.cancelled")` -- cancelacion.
- `Error.Unavailable("speech.retry_exhausted")` -- reintentos agotados.

**Reintentos:** usa la configuracion de `SpeechSettings`: `RetryMaxAttempts` (por defecto 3), `RetryBaseDelayMs` (por defecto 300), y `BackoffType` (por defecto Exponential).

```csharp
var speech = serviceProvider.GetRequiredService<ICognitiveSpeechService>();

using var audioStream = File.OpenRead(@"grabacion.wav");

var result = await speech.SpeechToTextAsync(audioStream, ct);

if (result.IsSuccess)
    Console.WriteLine($"Reconocido: {result.Value}");
else
    _logger.LogError("Reconocimiento fallido: {Error}", result.Error);
```

#### SpeechToTextStreamAsync

Convierte un stream de audio a texto en tiempo real, transmitiendo fragmentos a medida que se reconocen.

```csharp
IAsyncEnumerable<Result<string>> SpeechToTextStreamAsync(
    Stream audioStream,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `audioStream` | Stream de audio en formato WAV/PCM. Se posiciona al inicio si es seekable. Devuelve `Error.Validation` si es `null`. |
| `cancellationToken` | Token de cancelacion. Detiene `StartContinuousRecognitionAsync`. |
| **Retorna** | `IAsyncEnumerable<Result<string>>` -- fragmentos de texto reconocidos en tiempo real. Fragmentos duplicados consecutivos se filtran automaticamente. |

**Posibles errores emitidos en la secuencia:**
- `Error.Validation("speech.audio.null")` -- stream nulo.
- `Error.Unavailable("speech.recognition.failed")` -- error en el reconocimiento.
- `Error.Cancelled("speech.cancelled")` -- cancelacion por el usuario.

```csharp
var speech = serviceProvider.GetRequiredService<ICognitiveSpeechService>();

using var audioStream = new MemoryStream(audioBytes);

await foreach (var chunk in speech.SpeechToTextStreamAsync(audioStream, ct))
{
    if (chunk.IsFailure)
    {
        _logger.LogError("Error: {Error}", chunk.Error);
        break;
    }
    Console.WriteLine($"Fragmento: {chunk.Value}");
}
```

---

## Implementacion interna

### Constructor de AzureCognitiveSpeechService

```csharp
internal class AzureCognitiveSpeechService : ICognitiveSpeechService
{
    private readonly SpeechConfig _speechConfig;
    private readonly SpeechSettings _speechSettings;
    private readonly ILogger<AzureCognitiveSpeechService> _logger;
    private readonly IBlobStorageServiceFactory _blobStorageServiceFactory;
    private readonly AudioStreamFormat _audioStreamFormat;

    public AzureCognitiveSpeechService(
        IBlobStorageServiceFactory blobStorageServiceFactory,
        IOptions<BaseApplicationSettings> settings,
        ILogger<AzureCognitiveSpeechService> logger)
    {
        _speechSettings = settings.Value.SpeechSettings ?? new SpeechSettings();

        _speechConfig = SpeechConfig.FromSubscription(_speechSettings.Key, _speechSettings.Region);
        _speechConfig.SpeechSynthesisLanguage = _speechSettings.Language;
        _speechConfig.SpeechRecognitionLanguage = _speechSettings.Language;

        if (!string.IsNullOrWhiteSpace(_speechSettings.VoiceName))
            _speechConfig.SpeechSynthesisVoiceName = _speechSettings.VoiceName;

        if (Enum.TryParse<SpeechSynthesisOutputFormat>(_speechSettings.OutputFormat, true, out var fmt))
            _speechConfig.SetSpeechSynthesisOutputFormat(fmt);

        _audioStreamFormat = AudioStreamFormat.GetWaveFormatPCM(
            _speechSettings.SampleRate ?? 16000,
            _speechSettings.BitsPerSample ?? 16,
            _speechSettings.Channels ?? 1);
    }
}
```

El constructor inicializa:
- `SpeechConfig` con Key, Region, Language, VoiceName y OutputFormat desde settings.
- `AudioStreamFormat` con SampleRate (default 16000), BitsPerSample (default 16), Channels (default 1) para conversion de streams de audio.
- `IBlobStorageServiceFactory` para la cache de audio en Blob Storage.
- El mismo `Language` se usa tanto para sintesis como para reconocimiento.

### Metodos privados clave

| Metodo | Funcion |
|---|---|
| `DelayForRetryAsync(int, CancellationToken)` | Calcula el retraso del reintento segun `BackoffType` (constant/linear/exponential), usando `RetryBaseDelayMs`. |
| `BuildCacheFileName(string)` | Genera nombre unico de blob combinando `audioName` con hash SHA256 de `VoiceName + Language + OutputFormat + SampleRate + BitsPerSample + Channels`. |
| `ResolveContentType(string?)` | Mapea `OutputFormat` a MIME type: `mp3` -> `audio/mpeg`, `ogg` -> `audio/ogg`, otros -> `audio/wav`. |
| `ResolveExtension(string?)` | Mapea `OutputFormat` a extension de archivo: `mp3` -> `.mp3`, `ogg` -> `.ogg`, otros -> `.wav`. |

### Logica de TextToSpeechStreamAsync

```
TextToSpeechStreamAsync(text, ct)
  |
  +--> Validar texto no vacio -> Error.Validation si vacio
  |
  +--> Crear SpeechSynthesizer
  |
  +--> Channel<Result<byte[]>> (unbounded)
  |     |
  |     +--> Evento Synthesizing -> channel.Write(Result.Success(audioData))
  |
  +--> speakTask = synthesizer.SpeakTextAsync(text)
  +--> delayTask = Task.Delay(Infinite, timeoutCts con TimeoutSeconds)
  |
  +--> Task.WhenAny(speakTask, delayTask)
  |     |
  |     +--> Gano delayTask (timeout):
  |     |      - synthesizer.StopSpeakingAsync()
  |     |      - Si ct cancelado -> Error.Cancelled
  |     |      - Si timeout -> Error.Timeout
  |     |
  |     +--> Gano speakTask:
  |            - Si ResultReason != SynthesizingAudioCompleted -> Error.Unavailable
  |
  +--> Captura OperationCanceledException -> Error.Cancelled
  +--> Captura Exception -> Error.Internal
  |
  +--> finally: channel.Complete() + synthesizer.Dispose()
  |
  +--> yield foreach channel.Reader.ReadAllAsync()
```

### Logica de TextToSpeechCacheableStreamAsync

```
TextToSpeechCacheableStreamAsync(text, blobStorageName, audioName, ct)
  |
  +--> Validar texto no vacio
  |
  +--> blobStorage = factory.CreateAsync(blobStorageName, forceCreateContainer: true)
  +--> fileName = BuildCacheFileName(audioName)
  |
  +--> blobStorage.ExistsAsync(fileName)?
  |     SI -> yield foreach blobStorage.DownloadAsyncEnumerableAsync(fileName)
  |     NO -> Continuar
  |
  +--> memoryStream = new MemoryStream()
  |
  +--> await foreach chunk en TextToSpeechStreamAsync(text, ct):
  |     +--> Si chunk.IsFailure: yield error y salir
  |     +--> memoryStream.Write(chunk)
  |     +--> yield chunk (streaming en tiempo real)
  |
  +--> memoryStream.Seek(0)
  +--> tempName = fileName + ".tmp-" + Guid.NewGuid()
  +--> contentType = ResolveContentType(outputFormat)
  |
  +--> blobStorage.UploadAsync(tempName, memoryStream, contentType)
  +--> memoryStream.Seek(0)
  +--> blobStorage.UploadAsync(fileName, memoryStream, contentType, overwrite: false)
  +--> blobStorage.DeleteAsync(tempName)
```

La escritura en Blob Storage es atomica: sube a un archivo temporal, luego al archivo final con `overwrite: false` (protege contra race conditions si dos hilos intentan cachear el mismo audio), y finalmente borra el temporal.

### Logica de SpeechToTextAsync

```
SpeechToTextAsync(audioStream, ct)
  |
  +--> Validar stream no nulo
  |
  +--> for attempt = 1 .. RetryMaxAttempts:
         |
         +--> Si CanSeek -> audioStream.Position = 0
         |    Si no y attempt > 1 -> Error.Validation (no seekable, no retry)
         |
         +--> fullText = new StringBuilder()
         +--> await foreach chunk en SpeechToTextStreamAsync(audioStream, ct):
         |      +--> Si chunk.IsFailure -> return Error
         |      +--> fullText.Append(chunk.Value)
         |
         +--> return Result.Success(fullText.ToString())
         |
         +--> catch OperationCanceledException -> Error.Cancelled
         +--> catch Exception:
                +--> Si attempt == RetryMaxAttempts -> Error.Unavailable
                +--> Sino: log warning, DelayForRetryAsync(attempt), continuar loop
```

### Logica de SpeechToTextStreamAsync

```
SpeechToTextStreamAsync(audioStream, ct)
  |
  +--> Validar stream no nulo
  +--> Si CanSeek -> audioStream.Position = 0
  |
  +--> AudioStreamHelper.ConvertToAudioInputStream(audioStream, audioStreamFormat)
  +--> AudioConfig.FromStreamInput(audioInputStream)
  +--> new SpeechRecognizer(speechConfig, audioInput)
  |
  +--> Channel<Result<string>> (unbounded)
  |     |
  |     +--> Evento Recognized -> Si texto != lastText -> channel.Write(text)
  |     +--> Evento Canceled  -> channel.Write(error) + channel.Complete()
  |     +--> Evento SessionStopped -> channel.Complete()
  |
  +--> recognizer.StartContinuousRecognitionAsync()
  |
  +--> yield foreach channel.Reader.ReadAllAsync(ct)
  |
  +--> finally: recognizer.StopContinuousRecognitionAsync()
```

### AudioStreamHelper

```csharp
public static class AudioStreamHelper
{
    public static AudioInputStream ConvertToAudioInputStream(
        Stream stream, AudioStreamFormat? format = null)
    {
        var reader = BinaryAudioStreamReader.Create(stream);
        var audioFormat = format ?? AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
        return AudioInputStream.CreatePullStream(reader, audioFormat);
    }
}
```

Convierte un `Stream` de .NET en un `AudioInputStream` de Azure Speech SDK usando un `PullAudioInputStreamCallback`. Si no se especifica formato, usa PCM 16kHz mono 16bit por defecto.

### Health check

```csharp
internal class AzureSpeechHealthCheck(IOptions<BaseApplicationSettings> settings)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient
            {
                BaseAddress = new Uri(
                    $"https://{settings.Value.SpeechSettings?.Region}.api.cognitive.microsoft.com/")
            };
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key",
                settings.Value.SpeechSettings?.Key);

            var response = await client.GetAsync(
                "speechtotext/v3.0/healthstatus", ct);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Azure Speech Service esta disponible.")
                : HealthCheckResult.Unhealthy("Error en Azure Speech Service.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Error en Azure Speech Service.", ex);
        }
    }
}
```

---

## Modelo de reintentos

`SpeechToTextAsync` aplica reintentos automaticos con backoff configurable via `SpeechSettings`:

| Parametro | Default | Descripcion |
|---|---|---|
| `RetryMaxAttempts` | 3 | Numero maximo de intentos (incluyendo el inicial) |
| `RetryBaseDelayMs` | 300 | Retraso base en milisegundos |
| `BackoffType` | Exponential | Estrategia: `constant` (mismo delay), `linear` (delay * intento), `exponential` (delay * 2^(intento-1)) |

### Restriccion de reintentos con streams no seekables

Si `audioStream.CanSeek` es `false`, despues del primer intento la posicion del stream no se puede resetear. El metodo devuelve `Error.Validation("speech.audio.not_seekable")` en lugar de reintentar sin datos validos.

```csharp
// Stream seekable (FileStream, MemoryStream) -> reintentos OK
using var stream = File.OpenRead("audio.wav");

// Stream no seekable (NetworkStream, PipeReader) -> solo 1 intento
// Si falla, devuelve Error.Validation por no poder reintentar
```

---

## Modelo de cache de audio

`TextToSpeechCacheableStreamAsync` usa Blob Storage como cache de larga duracion. La clave de cache se genera con:

```
SHA256(audioName | VoiceName | Language | OutputFormat | SampleRate | BitsPerSample | Channels)
```

Esto significa que:
- Cambiar cualquier parametro de voz (voz, idioma, formato, calidad) genera una cache distinta.
- El mismo texto con los mismos parametros produce el mismo hash y reutiliza la cache.
- El nombre de archivo final es: `{audioName}-{hash}.{extension}`.

### Escritura atomica

Para evitar blobs corruptos por cancelaciones o fallos durante la subida, la cache se escribe en dos pasos:

1. Subir a `{fileName}.tmp-{Guid.NewGuid():N}`
2. Subir a `{fileName}` con `overwrite: false`
3. Borrar `{fileName}.tmp-*`

Si el paso 2 falla porque otro hilo ya escribio el archivo (409 Conflict), el blob ya existe y es valido. Si el paso 3 falla, queda un archivo temporal huerfano que no afecta al funcionamiento.

---

## Health Check

El health check `azure_speech` (tag: `speech`) se registra automaticamente al llamar a `AddAzureCognitiveSpeechServices()`.

- Usa `HttpClient` contra `https://{Region}.api.cognitive.microsoft.com/speechtotext/v3.0/healthstatus`.
- **No crea recursos** de Azure.
- Si `Region` o `Key` son nulos, el health check falla con `Unhealthy`.

```csharp
// Se registra automaticamente:
services.AddAzureCognitiveSpeechServices();

// GET /healthz -> "azure_speech": Healthy/Unhealthy
```

---

## Consideraciones

### Formato de audio

El audio de entrada para `SpeechToText*` debe ser **WAV/PCM** con los parametros configurados en `SpeechSettings`:
- `SampleRate`: 16000 por defecto (8000, 16000, 24000, 48000 soportados)
- `BitsPerSample`: 16 por defecto
- `Channels`: 1 (mono) por defecto

Formatos comprimidos (MP3, OGG) no son soportados para reconocimiento de voz en esta implementacion.

### Timeout de sintesis

`TextToSpeechStreamAsync` usa `Task.WhenAny` con un timeout basado en `SpeechSettings.TimeoutSeconds` (default 30s). Si la sintesis no produce audio en ese tiempo, se detiene el `SpeechSynthesizer` y se emite `Error.Timeout`.

### Cancelacion de sintesis

Al cancelar via `CancellationToken`, el servicio ejecuta `synthesizer.StopSpeakingAsync()` y espera a que `speakTask` termine antes de liberar recursos. Esto evita leaks de conexiones WebSocket al SDK de Speech.

### Duplicados en streaming de reconocimiento

`SpeechToTextStreamAsync` mantiene una referencia a `lastText` y solo emite fragmentos cuando el texto cambia. El SDK de Azure puede emitir el mismo texto parcial multiples veces durante el reconocimiento continuo.

### Sin reintentos en streaming

`SpeechToTextStreamAsync` **no tiene reintentos**. La responsabilidad de reintentar recae en el consumidor (ej. `SpeechToTextAsync` que si reintenta llamando a `SpeechToTextStreamAsync` de nuevo).

### Sin sintesis sin cache

No existe una sobrecarga sincrona de `TextToSpeech*`. Todo el audio se produce y consume en streaming. Si necesitas el audio completo en memoria, acumula los chunks en un `MemoryStream`.

---

## Testing

### Mock de ICognitiveSpeechService

```csharp
var mockSpeech = new Mock<ICognitiveSpeechService>();

// TextToSpeechStreamAsync
mockSpeech.Setup(s => s.TextToSpeechStreamAsync(
        It.IsAny<string>(), It.IsAny<CancellationToken>()))
    .Returns(new[] { Result<byte[]>.Success(new byte[] { 0x01, 0x02 }) }
        .ToAsyncEnumerable());

// TextToSpeechCacheableStreamAsync
mockSpeech.Setup(s => s.TextToSpeechCacheableStreamAsync(
        It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
        It.IsAny<CancellationToken>()))
    .Returns(new[] { Result<byte[]>.Success(new byte[] { 0x01 }) }
        .ToAsyncEnumerable());

// SpeechToTextAsync - exito
mockSpeech.Setup(s => s.SpeechToTextAsync(
        It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<string>.Success("texto reconocido"));

// SpeechToTextAsync - fallo
mockSpeech.Setup(s => s.SpeechToTextAsync(
        It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<string>.Failure(
        Error.Unavailable("speech.retry_exhausted", "fallo")));

// SpeechToTextStreamAsync
mockSpeech.Setup(s => s.SpeechToTextStreamAsync(
        It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
    .Returns(new[] {
        Result<string>.Success("hola"),
        Result<string>.Success("mundo")
    }.ToAsyncEnumerable());
```

---

## Ejemplos de uso

### Sintetizar audio y transmitirlo en respuesta HTTP

```csharp
app.MapGet("/api/speech/synthesize", async (
    string text, ICognitiveSpeechService speech, HttpResponse response, CancellationToken ct) =>
{
    response.ContentType = "audio/wav";

    await foreach (var chunk in speech.TextToSpeechStreamAsync(text, ct))
    {
        if (chunk.IsFailure)
            return Results.BadRequest(chunk.Error.Message);

        await response.Body.WriteAsync(chunk.Value!, ct);
        await response.Body.FlushAsync(ct);
    }

    return Results.Ok();
});
```

### Sintetizar con cache para respuestas recurrentes

```csharp
app.MapGet("/api/speech/welcome", async (
    ICognitiveSpeechService speech, HttpResponse response, CancellationToken ct) =>
{
    response.ContentType = "audio/wav";

    await foreach (var chunk in speech.TextToSpeechCacheableStreamAsync(
        "Bienvenido a Akay.Be", "speech-cache", "welcome-es", ct))
    {
        if (chunk.IsFailure)
            return Results.BadRequest(chunk.Error.Message);

        await response.Body.WriteAsync(chunk.Value!, ct);
    }

    return Results.Ok();
});
```

### Reconocer audio subido por el usuario

```csharp
app.MapPost("/api/speech/recognize", async (
    IFormFile audio, ICognitiveSpeechService speech, CancellationToken ct) =>
{
    using var stream = audio.OpenReadStream();

    var result = await speech.SpeechToTextAsync(stream, ct);

    return result.IsSuccess
        ? Results.Ok(new { text = result.Value })
        : Results.BadRequest(new { error = result.Error.Message });
});
```

### Reconocimiento en tiempo real con streaming

```csharp
app.MapPost("/api/speech/recognize-stream", async (
    ICognitiveSpeechService speech, HttpResponse response, CancellationToken ct) =>
{
    response.ContentType = "text/plain";

    using var audioStream = new MemoryStream();
    await Request.Body.CopyToAsync(audioStream, ct);
    audioStream.Position = 0;

    await foreach (var chunk in speech.SpeechToTextStreamAsync(audioStream, ct))
    {
        if (chunk.IsFailure)
            break;

        await response.WriteAsync(chunk.Value + "\n", ct);
        await response.Body.FlushAsync(ct);
    }

    return Results.Ok();
});
```
