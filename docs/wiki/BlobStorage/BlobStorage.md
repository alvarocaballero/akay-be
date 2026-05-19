# Blob Storage

## Que es

`IBlobStorageService` es una abstraccion sobre `Azure.Storage.Blobs` que proporciona operaciones CRUD de blobs, compresion transparente, generacion de SAS, copia entre blobs y gestion de metadatos, sin dependencia directa del SDK en la capa de aplicacion.

Cada instancia del servicio esta ligada a un contenedor concreto y se obtiene via factory.

**Paquete:** `Akay.To.Azure`
**Interfaz:** `Akay.To.Core.Application.Abstractions.BlobStorage.IBlobStorageService`
**Implementacion:** `Akay.To.Azure.Infrastructure.Services.AzureBlobStorageService`
**Registro DI:** `Akay.To.Azure.Infrastructure.DependencyInjection.AzureBlobStorageConfiguration`

---

## Por que usarlo

- **Compresion transparente:** al subir con `compress: true`, el servicio aplica GZip, establece `ContentEncoding = gzip` y lo detecta automaticamente al descargar. No hay que recordar si un blob esta comprimido o no.
- **Abstraccion desacoplada:** la capa de aplicacion no referencia `Azure.Storage.Blobs`. Los tipos de Azure (`BlobProperties`, `BlobLeaseClient`) no se filtran a la interfaz.
- **SAS integrado:** genera URIs firmadas de solo lectura con HTTPS forzado, sin manipular builders manualmente.
- **Overwrite explicito:** por defecto **no sobreescribe** un blob existente (`overwrite = false`). Si necesitas reemplazar, pasas `overwrite: true`.
- **Validacion temprana:** nombres de contenedor y blob se validan en la entrada publica del servicio, con errores claros antes de llegar al SDK.
- **Control explicito de contenedores:** la creacion del contenedor es opcional (`forceCreateContainer`), evitando side effects en entornos con permisos limitados.
- **Copia entre blobs:** `CopyBlobAsync` genera automaticamente un SAS de lectura del origen para copiar blobs privados.
- **Health check sin side effects:** verifica conectividad con `BlobServiceClient.GetPropertiesAsync()`, sin crear contenedores.
- **Factory limpia:** `CreateAsync` devuelve el servicio ya inicializado, opcionalmente creando el contenedor. Sin bloqueos sincronos ni llamadas redundantes.

---

## Arquitectura

### IBlobStorageServiceFactory

```csharp
public interface IBlobStorageServiceFactory
{
    Task<IBlobStorageService> CreateAsync(
        string containerName,
        bool isPublicContainer = false,
        bool? compressContainer = null,
        bool forceCreateContainer = false,
        CancellationToken cancellationToken = default);
}
```

La factory es singleton. `CreateAsync` devuelve una instancia ya inicializada. No es necesario llamar a `SetContainerAsync` despues.

### IBlobStorageService (vista completa)

```csharp
public interface IBlobStorageService
{
    // --- Contenedor ---
    Task SetContainerAsync(string containerName, bool isPublic = false,
        bool? compress = null, bool forceCreateContainer = false,
        CancellationToken cancellationToken = default);

    // --- Upload ---
    Task<string> UploadAsync<T>(string blobName, T content,
        bool compress = false, bool overwrite = false,
        CancellationToken cancellationToken = default);
    Task<string> UploadAsync(string blobName, Stream fileStream,
        string? contentType, bool compress = false,
        bool overwrite = false, CancellationToken cancellationToken = default);
    Task<string> UploadOrGetUriAsync<T>(string blobName, T content,
        bool compress = false, CancellationToken cancellationToken = default);
    Task<string> UploadOrGetUriAsync(string blobName, Stream fileStream,
        string? contentType, bool compress = false,
        CancellationToken cancellationToken = default);

    // --- Download ---
    Task<T?> DownloadAsync<T>(string blobName, bool decompress = false,
        CancellationToken cancellationToken = default);
    Task<Stream?> DownloadStreamAsync(string blobName, bool decompress = false,
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<byte[]> DownloadAsyncEnumerableAsync(string blobName,
        CancellationToken cancellationToken = default);
    Task<string?> DownloadStringAsync(string blobName, bool decompress = false,
        CancellationToken cancellationToken = default);

    // --- Delete ---
    Task<bool> DeleteAsync(string blobName, CancellationToken cancellationToken = default);
    Task UnDeleteAsync(string blobName, CancellationToken cancellationToken = default);

    // --- List & Exist ---
    Task<List<string>> BlobsNameAsync(string prefix = "",
        CancellationToken cancellationToken = default);
    IAsyncEnumerable<BlobInformation> BlobsInfoAsync(BlobInfoType infoType,
        string prefix = "", CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string blobName, CancellationToken cancellationToken = default);

    // --- URL & SAS ---
    Uri GetBlobUri(string blobName);
    string GenerateReadSasUri(string blobName, DateTimeOffset? expiresOn = null);

    // --- Metadata ---
    Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(string blobName,
        CancellationToken cancellationToken = default);
    Task SetMetadataAsync(string blobName,
        IDictionary<string, string> metadata, CancellationToken cancellationToken = default);

    // --- Copy ---
    Task CopyBlobAsync(string sourceBlobName, string destinationBlobName,
        CancellationToken cancellationToken = default);
    Task<bool> WaitForCopyCompleteAsync(string destinationBlobName,
        TimeSpan timeout, CancellationToken cancellationToken = default);
}
```

### BlobInfoType y BlobInformation

```csharp
[Flags]
public enum BlobInfoType
{
    Default = 0,
    Metadata = 1,
    Tags = 2,
    LegalHold = 4,
}

public record BlobInformation(
    string Name,
    bool IsDeleted,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyDictionary<string, string>? Tags = null);
```

---

## Configuracion

### Registro en DI

```csharp
using Akay.To.Azure.Infrastructure.DependencyInjection;

services.AddBlobStorage(settings);
```

### Que registra

| Servicio | Lifetime |
|---|---|
| `BlobServiceClient` | Singleton |
| `IBlobStorageServiceFactory` | Singleton |
| Health check `azure_blobs` (tag: `blob`) | Se anade al pipeline |

Si `AzureStorageSettings` es `null` o `ConnectionString` esta vacio, no se registra nada y el sistema opera sin blob storage (fail-safe).

### AzureStorageSettings

```csharp
public class AzureStorageSettings
{
    public string? ConnectionString { get; set; }
}
```

### appsettings.json

```json
{
  "AzureStorageSettings": {
    "ConnectionString": "UseDevelopmentStorage=true"
  }
}
```

---

## Guia de la API

### Factory: IBlobStorageServiceFactory

#### CreateAsync

```csharp
Task<IBlobStorageService> CreateAsync(
    string containerName,
    bool isPublicContainer = false,
    bool? compressContainer = null,
    bool forceCreateContainer = false,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `containerName` | Nombre del contenedor Azure. Se valida que no sea nulo/vacio. |
| `isPublicContainer` | Si es `true`, crea el contenedor con acceso `Blob` (lectura anonima de blobs individuales, sin listar). |
| `compressContainer` | Si es `true`, todos los blobs del contenedor se comprimen por defecto. El parametro `compress` de `UploadAsync` tiene prioridad. Si es `false`, no se comprime por defecto. Si es `null`, no se comprime. |
| `forceCreateContainer` | Si es `true`, crea el contenedor si no existe (`CreateIfNotExistsAsync`). Si es `false`, solo obtiene el cliente sin crear nada. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `IBlobStorageService` ya inicializado y listo para usar. No es necesario llamar a `SetContainerAsync` despues. |

La factory es singleton y stateless: cada llamada instancia un nuevo servicio independiente ligado al contenedor indicado.

```csharp
// Obtener servicio para el contenedor "avatars", creandolo si no existe
var blob = await factory.CreateAsync("avatars", forceCreateContainer: true, cancellationToken: ct);

// Obtener servicio para contenedor con compresion por defecto
var blob = await factory.CreateAsync("logs", compressContainer: true, cancellationToken: ct);

// Obtener servicio para contenedor publico con acceso de solo lectura anonimo
var blob = await factory.CreateAsync("assets", isPublicContainer: true, forceCreateContainer: true, cancellationToken: ct);
```

---

### Contenedor

#### SetContainerAsync

Cambia el contenedor al que apunta el servicio. Util cuando necesitas reutilizar la misma instancia con varios contenedores.

```csharp
Task SetContainerAsync(
    string containerName,
    bool isPublic = false,
    bool? compress = null,
    bool forceCreateContainer = false,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `containerName` | Nombre del contenedor. Lanza `ArgumentException` si es nulo o whitespace. |
| `isPublic` | Acceso publico `Blob` si es `true`. |
| `compress` | Compresion por defecto para este contenedor. |
| `forceCreateContainer` | Crea el contenedor si no existe. |
| `cancellationToken` | Token de cancelacion. |

```csharp
var blob = await factory.CreateAsync("temp", cancellationToken: ct);

// Cambiar a otro contenedor sin crear nueva instancia
await blob.SetContainerAsync("archive", forceCreateContainer: true, cancellationToken: ct);

// Ahora todas las operaciones usan el contenedor "archive"
await blob.UploadAsync("data.json", payload, cancellationToken: ct);
```

#### ContainerName

Propiedad de solo lectura que expone el nombre del contenedor actual.

```csharp
var blob = await factory.CreateAsync("documents", cancellationToken: ct);
Console.WriteLine(blob.ContainerName); // "documents"
```

---

### Subida

#### UploadAsync\<T\>

Serializa un objeto a JSON (camelCase) y lo sube como blob con `Content-Type: application/json`.

```csharp
Task<string> UploadAsync<T>(
    string blobName,
    T content,
    bool compress = false,
    bool overwrite = false,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. Lanza `ArgumentException` si es nulo o whitespace. |
| `content` | Objeto a serializar. Cualquier tipo serializable por `System.Text.Json`. |
| `compress` | Si es `true`, comprime con GZip (`CompressionLevel.Optimal`), establece `ContentEncoding = gzip` y metadata `Compressed = true`. Prioridad sobre `compressContainer`. |
| `overwrite` | Si es `false` (por defecto), lanza `RequestFailedException` (409 Conflict) si el blob ya existe. Si es `true`, lo reemplaza. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | URI completa del blob subido. |

```csharp
var blob = await factory.CreateAsync("profiles", forceCreateContainer: true, ct);

var user = new UserProfile { Id = Guid.NewGuid(), Name = "Alice" };

// Subir comprimido (recomendado para JSON > 1 KB)
string uri = await blob.UploadAsync($"users/{user.Id}/profile.json", user, compress: true, ct);

// Si el blob ya existe, falla con 409
// await blob.UploadAsync("key", data, ct); // RequestFailedException

// Para reemplazar
await blob.UploadAsync("key", data, overwrite: true, ct);
```

#### UploadAsync (Stream)

Sube un stream con un Content-Type opcional.

```csharp
Task<string> UploadAsync(
    string blobName,
    Stream fileStream,
    string? contentType,
    bool compress = false,
    bool overwrite = false,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `fileStream` | Stream de datos a subir. Si `compress = true` y `CanSeek`, la posicion se resetea a 0 antes de comprimir. |
| `contentType` | MIME type del contenido (ej. `"image/png"`, `"application/pdf"`). Si es `null`, no se establece `Content-Type`. |
| `compress` | Comprime con GZip antes de subir. |
| `overwrite` | Si es `false`, lanza 409 si el blob ya existe. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | URI completa del blob subido. |

```csharp
var blob = await factory.CreateAsync("images", forceCreateContainer: true, ct);

using var fileStream = File.OpenRead(@"C:\photos\avatar.png");

// Subir sin comprimir (la imagen ya esta comprimida)
string uri = await blob.UploadAsync(
    "users/123/avatar.png", fileStream, "image/png", ct);

// Subir un log comprimido
using var logStream = new MemoryStream(Encoding.UTF8.GetBytes(hugeLog));
string uri = await blob.UploadAsync(
    "logs/app.log", logStream, "text/plain", compress: true, ct);
```

#### UploadOrGetUriAsync\<T\>

Subida idempotente: si el blob ya existe devuelve su URI en lugar de lanzar excepcion.

```csharp
Task<string> UploadOrGetUriAsync<T>(
    string blobName,
    T content,
    bool compress = false,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `content` | Objeto a serializar. |
| `compress` | Si es `true`, comprime con GZip. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | URI del blob (nuevo o existente). |

```csharp
public async Task<string> CachePayloadAsync(string key, Payload data, CancellationToken ct)
{
    var blob = await factory.CreateAsync("cache", ct);

    // Si el blob no existe, lo crea. Si ya existe, devuelve su URI.
    return await blob.UploadOrGetUriAsync(key, data, compress: true, ct);
}
```

#### UploadOrGetUriAsync (Stream)

Sobrecarga identica para streams.

```csharp
Task<string> UploadOrGetUriAsync(
    string blobName,
    Stream fileStream,
    string? contentType,
    bool compress = false,
    CancellationToken cancellationToken = default);
```

```csharp
using var stream = GenerateReportPdf();

var blob = await factory.CreateAsync("reports", ct);
string uri = await blob.UploadOrGetUriAsync("monthly/report.pdf", stream, "application/pdf", ct);

// La primera llamada sube el blob; las siguientes devuelven la URI existente.
```

---

### Descarga

#### DownloadAsync\<T\>

Descarga un blob JSON y lo deserializa al tipo indicado.

```csharp
Task<T?> DownloadAsync<T>(
    string blobName,
    bool decompress = false,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `decompress` | Si es `true`, fuerza la descompresion GZip. Si es `false`, la descompresion se detecta automaticamente. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `T?` — el objeto deserializado, o `null`/`default` si el blob no existe. |

```csharp
var blob = await factory.CreateAsync("profiles", ct);

UserProfile? profile = await blob.DownloadAsync<UserProfile>(
    $"users/{userId}/profile.json", ct);

if (profile is not null)
    Console.WriteLine(profile.Name);
```

#### DownloadStreamAsync

Descarga el contenido de un blob como `Stream`.

```csharp
Task<Stream?> DownloadStreamAsync(
    string blobName,
    bool decompress = false,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `decompress` | Fuerza descompresion si es `true`. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Stream?` — el contenido del blob, o `null` si no existe. **El caller debe disponer el stream.** |

```csharp
var blob = await factory.CreateAsync("images", ct);

Stream? imageStream = await blob.DownloadStreamAsync(
    $"users/{userId}/avatar.png", ct);

if (imageStream is not null)
{
    using (imageStream)
    {
        // Procesar el stream...
    }
}
```

#### DownloadAsyncEnumerableAsync

Descarga un blob en chunks de **8192 bytes**. Util para archivos grandes sin cargarlos completos en memoria. La descompresion se detecta automaticamente (no tiene parametro `decompress`).

```csharp
IAsyncEnumerable<byte[]> DownloadAsyncEnumerableAsync(
    string blobName,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `cancellationToken` | Token de cancelacion. Admite `[EnumeratorCancellation]`. |
| **Retorna** | `IAsyncEnumerable<byte[]>` — secuencia de chunks. Cada chunk es un array de bytes (hasta 8192). |

```csharp
var blob = await factory.CreateAsync("data", ct);

await foreach (var chunk in blob.DownloadAsyncEnumerableAsync("large-file.bin", ct))
{
    await outputStream.WriteAsync(chunk, ct);
}
```

#### DownloadStringAsync

Descarga el contenido de un blob como string UTF-8.

```csharp
Task<string?> DownloadStringAsync(
    string blobName,
    bool decompress = false,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `decompress` | Fuerza descompresion si es `true`. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `string?` — el contenido del blob, o `null` si no existe. |

```csharp
var blob = await factory.CreateAsync("logs", ct);

string? logContent = await blob.DownloadStringAsync("app.log", ct);
```

---

### Eliminacion

#### DeleteAsync

Elimina un blob (soft-delete si la cuenta lo soporta).

```csharp
Task<bool> DeleteAsync(
    string blobName,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `true` si el blob existia y fue eliminado, `false` si no existia. |

```csharp
var blob = await factory.CreateAsync("temp", ct);

bool deleted = await blob.DeleteAsync("expired-file.json", ct);
// deleted = true si existia, false si no
```

#### UnDeleteAsync

Restaura un blob eliminado via soft-delete. **Lanza excepcion** si el blob no estaba en estado soft-deleted.

```csharp
Task UnDeleteAsync(
    string blobName,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `cancellationToken` | Token de cancelacion. |

```csharp
var blob = await factory.CreateAsync("temp", ct);

await blob.DeleteAsync("important-file.json", ct);

// Restaurarlo despues de un eliminado accidental
await blob.UnDeleteAsync("important-file.json", ct);
```

---

### Listado y existencia

#### BlobsNameAsync

Lista los nombres de los blobs dentro del contenedor, opcionalmente filtrados por prefijo.

```csharp
Task<List<string>> BlobsNameAsync(
    string prefix = "",
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `prefix` | Prefijo para filtrar (actua como ruta virtual). `""` lista todos los blobs del contenedor. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `List<string>` con los nombres de los blobs. |
| **Excepciones** | `InvalidOperationException` si no se ha establecido un contenedor. |

```csharp
var blob = await factory.CreateAsync("documents", ct);

// Todos los blobs del contenedor
List<string> all = await blob.BlobsNameAsync(ct);

// Solo los de un usuario
List<string> userFiles = await blob.BlobsNameAsync("users/123/", ct);
```

#### BlobsInfoAsync

Lista blobs con informacion enriquecida (metadatos, tags, legal hold) en streaming.

```csharp
IAsyncEnumerable<BlobInformation> BlobsInfoAsync(
    BlobInfoType infoType,
    string prefix = "",
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `infoType` | Flags `BlobInfoType` indicando que datos extra incluir. |
| `prefix` | Prefijo para filtrar. |
| `cancellationToken` | Token de cancelacion. Admite `[EnumeratorCancellation]`. |
| **Retorna** | `IAsyncEnumerable<BlobInformation>` para consumo en streaming. |

```csharp
[Flags]
public enum BlobInfoType
{
    Default = 0,
    Metadata = 1,
    Tags = 2,
    LegalHold = 4,
}

public record BlobInformation(
    string Name,
    bool IsDeleted,
    IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyDictionary<string, string>? Tags = null);
```

```csharp
var blob = await factory.CreateAsync("documents", ct);

// Solo nombres
await foreach (var info in blob.BlobsInfoAsync(BlobInfoType.Default, "users/", ct))
    Console.WriteLine(info.Name);

// Con metadatos y tags
await foreach (var info in blob.BlobsInfoAsync(
    BlobInfoType.Metadata | BlobInfoType.Tags, "users/", ct))
{
    Console.WriteLine($"{info.Name} | Deleted={info.IsDeleted}");
    foreach (var kv in info.Metadata ?? [])
        Console.WriteLine($"  {kv.Key}={kv.Value}");
}
```

#### ExistsAsync

Verifica si un blob existe.

```csharp
Task<bool> ExistsAsync(
    string blobName,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `true` si el blob existe, `false` en caso contrario. |

```csharp
var blob = await factory.CreateAsync("data", ct);

if (await blob.ExistsAsync("config/settings.json", ct))
{
    var settings = await blob.DownloadAsync<AppSettings>("config/settings.json", ct);
}
```

---

### URLs y SAS

#### GetBlobUri

Devuelve la URI publica del blob (sin token SAS).

```csharp
Uri GetBlobUri(string blobName);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| **Retorna** | `Uri` del blob. Es sincrono (no requiere `await`). |

```csharp
var blob = await factory.CreateAsync("assets", ct);

Uri blobUri = blob.GetBlobUri("images/logo.png");
// blobUri = "https://storageaccount.blob.core.windows.net/assets/images/logo.png"
```

#### GenerateReadSasUri

Genera una URI firmada (SAS) con permisos de solo lectura, HTTPS forzado, y expiracion configurable.

```csharp
string GenerateReadSasUri(
    string blobName,
    DateTimeOffset? expiresOn = null);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `expiresOn` | Fecha de expiracion del token. Por defecto: `DateTimeOffset.UtcNow.AddHours(1)`. |
| **Retorna** | `string` con la URI completa incluyendo el token SAS. |
| **Excepciones** | `InvalidOperationException` si el cliente usa Managed Identity (no puede generar SAS con Shared Key). |

El SAS generado tiene las siguientes caracteristicas:
- Permisos: solo lectura (`sp=r`)
- Protocolo: solo HTTPS (`spr=https`)
- Recurso: blob individual (`b`)
- Expiracion: 1 hora por defecto

```csharp
var blob = await factory.CreateAsync("documents", ct);

// SAS que expira en 30 minutos
string sasUrl = blob.GenerateReadSasUri(
    $"users/{userId}/report.pdf",
    expiresOn: DateTimeOffset.UtcNow.AddMinutes(30));

// sasUrl = "https://.../users/123/report.pdf?sv=...&spr=https&sr=b&..."
```

---

### Metadatos

#### GetMetadataAsync

Obtiene todos los metadatos de un blob como diccionario de solo lectura.

```csharp
Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(
    string blobName,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `IReadOnlyDictionary<string, string>` con los pares clave-valor de metadatos. |

```csharp
var blob = await factory.CreateAsync("data", ct);

var metadata = await blob.GetMetadataAsync("reports/summary.json", ct);

if (metadata.TryGetValue("version", out var version))
    Console.WriteLine($"Version: {version}");
```

#### SetMetadataAsync

Reemplaza **todos** los metadatos del blob. No es aditivo: cualquier clave no incluida en el nuevo diccionario se elimina.

```csharp
Task SetMetadataAsync(
    string blobName,
    IDictionary<string, string> metadata,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `blobName` | Nombre/ruta del blob. |
| `metadata` | Diccionario con los nuevos metadatos. Reemplaza los existentes. |
| `cancellationToken` | Token de cancelacion. |

```csharp
var blob = await factory.CreateAsync("data", ct);

await blob.SetMetadataAsync("reports/summary.json", new Dictionary<string, string>
{
    ["project"] = "Akay",
    ["version"] = "2.1.0",
    ["generatedAt"] = DateTime.UtcNow.ToString("O")
}, ct);
```

---

### Copia

#### CopyBlobAsync

Copia un blob dentro del mismo contenedor. Internamente genera un SAS de lectura del origen, por lo que funciona incluso con blobs privados. La copia es server-side (no descarga ni re-sube datos).

```csharp
Task CopyBlobAsync(
    string sourceBlobName,
    string destinationBlobName,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `sourceBlobName` | Nombre/ruta del blob origen. |
| `destinationBlobName` | Nombre/ruta del blob destino. |
| `cancellationToken` | Token de cancelacion. |
| **Excepciones** | `InvalidOperationException` si no hay contenedor establecido. |

```csharp
var blob = await factory.CreateAsync("data", ct);

// Copia server-side (no descarga el contenido)
await blob.CopyBlobAsync("reports/current.json", "reports/backup.json", ct);
```

#### WaitForCopyCompleteAsync

Espera a que una operacion de copia asincrona finalice, sondeando el estado cada segundo.

```csharp
Task<bool> WaitForCopyCompleteAsync(
    string destinationBlobName,
    TimeSpan timeout,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `destinationBlobName` | Nombre/ruta del blob destino (el que se esta copiando). |
| `timeout` | Tiempo maximo de espera. Si se supera, lanza `TimeoutException`. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `true` cuando la copia finaliza con exito. |
| **Excepciones** | `TimeoutException` si se agota el timeout. `InvalidOperationException` si la copia falla o se aborta. |

```csharp
var blob = await factory.CreateAsync("data", ct);

string source = "reports/large-report.pdf";
string dest = $"archive/{DateTime.UtcNow:yyyy/MM/dd}/report.pdf";

await blob.CopyBlobAsync(source, dest, ct);

// Esperar hasta 2 minutos a que termine la copia
bool completed = await blob.WaitForCopyCompleteAsync(dest, TimeSpan.FromMinutes(2), ct);

if (completed)
{
    await blob.DeleteAsync(source, ct);
    Console.WriteLine("Archivado correctamente");
}
```

---

## Implementacion interna

### AzureBlobStorageServiceFactory

```csharp
public class AzureBlobStorageServiceFactory(BlobServiceClient serviceClient)
    : IBlobStorageServiceFactory
{
    private readonly BlobServiceClient _serviceClient = serviceClient;

    public async Task<IBlobStorageService> CreateAsync(
        string containerName, bool isPublicContainer = false,
        bool? compressContainer = null, bool forceCreateContainer = false,
        CancellationToken cancellationToken = default)
    {
        var service = new AzureBlobStorageService(
            _serviceClient, containerName, compressContainer);

        await service.SetContainerAsync(containerName, isPublicContainer,
            compressContainer, forceCreateContainer, cancellationToken);

        return service;
    }
}
```

La factory es **stateless**: cada llamada instancia un nuevo `AzureBlobStorageService` y llama a `SetContainerAsync` para inicializarlo. El `BlobServiceClient` (singleton) se comparte entre todas las instancias.

### AzureBlobStorageService

```csharp
public class AzureBlobStorageService : IBlobStorageService
{
    private const string ContainerNotSelected = "Container not selected";

    private readonly BlobServiceClient _serviceClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private BlobContainerClient? _containerClient;
    private bool? _compressContainer;

    public string? ContainerName { get; private set; }

    public AzureBlobStorageService(
        BlobServiceClient serviceClient,
        string containerName,
        bool? compressContainer = null)
    {
        _serviceClient = serviceClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        ContainerName = containerName;
        _containerClient = _serviceClient.GetBlobContainerClient(containerName);
        _compressContainer = compressContainer;
    }
}
```

El constructor inicializa `JsonSerializerOptions` con camelCase, case-insensitive y sin indentado (optimizado para almacenamiento), resuelve el `BlobContainerClient` y almacena la preferencia de compresion del contenedor.

### Metodos privados clave

| Metodo | Funcion |
|---|---|
| `ValidateContainerName(string)` | Lanza `ArgumentException` si es nulo o whitespace |
| `ValidateBlobName(string)` | Lanza `ArgumentException` si es nulo o whitespace |
| `IsMustCompress(bool?)` | Resuelve prioridad: parametro `compress` > `_compressContainer` > `false` |
| `ShouldDecompress(BlobProperties, bool)` | Detecta descompresion: (1) `decompressRequested`, (2) `ContentEncoding == gzip`, (3) metadata `Compressed == true` |
| `BuildCompressedMetadata()` | Crea diccionario `{ Compressed = true }` |
| `GetBlobClient(string)` | Obtiene `BlobClient` desde `_containerClient`; lanza `InvalidOperationException(ContainerNotSelected)` si no hay contenedor |

### Helpers de compresion

```csharp
// Datos en memoria (byte[])
CompressData(byte[])    → MemoryStream + GZipStream(CompressionLevel.Optimal) → byte[]
DecompressData(byte[])  → MemoryStream + GZipStream(Decompress) → byte[]

// Streams
CompressStreamToMemory(Stream)  → resetea posicion si CanSeek, comprime, deja stream en posicion 0
DecompressStream(Stream)        → new GZipStream(compressedData, Decompress)
DecompressToMemory(Stream)      → descomprime a MemoryStream, deja en posicion 0
```

`CompressStreamToMemory` usa `leaveOpen: true` en el `GZipStream` para que el `MemoryStream` de salida sobreviva y pueda ser retornado.

### UploadOrGetUriAsync

```csharp
public async Task<string> UploadOrGetUriAsync<T>(
    string blobName, T content, bool compress = false,
    CancellationToken cancellationToken = default)
{
    try
    {
        return await UploadAsync(blobName, content, compress,
            overwrite: false, cancellationToken);
    }
    catch (RequestFailedException ex) when (ex.Status == 409)
    {
        var blobClient = GetBlobClient(blobName);
        if (!await blobClient.ExistsAsync(cancellationToken))
            throw;

        return blobClient.Uri.ToString();
    }
}
```

**Logica:** intenta subir con `overwrite: false`. Si el blob ya existe (HTTP 409), verifica que realmente exista (proteccion contra falsos 409) y devuelve la URI. Si el 409 no corresponde a un blob existente, relanza la excepcion. Util para patrones de cache y deduplicacion.

Existe sobrecarga identica para `Stream`.

### AzureBlobStorageHealthCheck

```csharp
internal class AzureBlobStorageHealthCheck(BlobServiceClient blobServiceClient)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await blobServiceClient.GetPropertiesAsync(
                cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy(
                "Azure Blob Storage esta disponible.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Error en Azure Blob Storage.", ex);
        }
    }
}
```

- Es `internal`: no se expone fuera de `Akay.To.Azure`.
- Usa `GetPropertiesAsync()` sobre `BlobServiceClient` (no sobre un contenedor): **no crea blobs ni contenedores**.
- Se registra con nombre `azure_blobs` y tag `blob`.
- Si `ConnectionString` esta vacio, el health check no se registra.

---

## Modelo de compresion

| Al subir | Almacenado como | Al descargar |
|---|---|---|
| `compress: true` | `ContentEncoding = gzip` + metadata `Compressed = true` | Descomprime automaticamente al detectar `ContentEncoding = gzip` |
| `compress: false` | Sin comprimir | Devuelve los bytes tal cual |

### Deteccion automatica en descarga

Al descargar, el servicio decide si descomprimir evaluando en orden:

1. Si el caller paso `decompress: true` explicitamente → descomprime.
2. Si el blob tiene `ContentEncoding = "gzip"` → descomprime.
3. Si el blob tiene metadata `Compressed = "true"` → descomprime.
4. En cualquier otro caso → no descomprime.

**No es necesario recordar si comprimiste al subir:** la descarga lo detecta sola.

### Prioridad de compresion en subida

`parametro compress de UploadAsync` > `compressContainer de la factory` > `false`

```csharp
// Contenedor con compresion por defecto
var blob = await factory.CreateAsync("logs", compressContainer: true, ct);

// Este se comprime (hereda del contenedor)
await blob.UploadAsync("app.log", logContent, ct);

// Este NO se comprime (el parametro tiene prioridad)
await blob.UploadAsync("app.log", logContent, compress: false, ct);
```

---

## Health Check

El health check `azure_blobs` (tag: `blob`) se registra automaticamente al llamar a `AddBlobStorage()`.

**Implementacion interna:**

```csharp
internal class AzureBlobStorageHealthCheck(BlobServiceClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        try
        {
            await client.GetPropertiesAsync(cancellationToken: ct);
            return HealthCheckResult.Healthy("Azure Blob Storage esta disponible.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error en Azure Blob Storage.", ex);
        }
    }
}
```

- Usa `BlobServiceClient.GetPropertiesAsync()`: **no crea contenedores ni blobs**.
- Es `internal`: no se expone fuera del paquete.
- Si `ConnectionString` esta vacio, no se registra.

```csharp
// Se registra automaticamente:
services.AddBlobStorage(settings);

// El endpoint de health checks incluira:
// GET /healthz --> "azure_blobs": Healthy/Unhealthy
```

---

## Consideraciones

### Overwrite

Por defecto, `UploadAsync` **no sobreescribe** blobs existentes (`overwrite = false`). Si el blob ya existe, Azure devuelve error 409 (Conflict). Para reemplazar, pasa `overwrite: true`.

```csharp
await blob.UploadAsync("key", content, ct);                   // Lanza 409 si existe
await blob.UploadAsync("key", content, overwrite: true, ct);  // Reemplaza si existe
```

### Contenedores publicos

El acceso publico se limita a `Blob` (no `BlobContainer`). Los blobs son legibles de forma anonima individualmente, pero no se puede listar el contenido del contenedor.

```csharp
var blob = await factory.CreateAsync("assets", isPublicContainer: true, forceCreateContainer: true, ct);
```

### Validacion de nombres

Todos los metodos publicos validan que `containerName` y `blobName` no sean nulos ni whitespace. Se lanza `ArgumentException` antes de llamar al SDK.

```csharp
await blob.UploadAsync("", content, ct);       // ArgumentException
await blob.SetContainerAsync(null!, ct);        // ArgumentException
```

### SAS y Managed Identity

`GenerateReadSasUri` requiere que el cliente se haya creado con **connection string** (Shared Key). Si usas Managed Identity / `DefaultAzureCredential`, `CanGenerateSasUri` sera `false` y el metodo lanzara `InvalidOperationException`.

Para Managed Identity, la alternativa es SAS de delegacion de usuario (`UserDelegationKey`), que no esta cubierto por esta abstraccion actualmente.

### Serializacion JSON

`UploadAsync<T>` y `DownloadAsync<T>` usan `System.Text.Json` con la siguiente configuracion:

| Opcion | Valor |
|---|---|
| `PropertyNameCaseInsensitive` | `true` |
| `WriteIndented` | `false` |
| `PropertyNamingPolicy` | `JsonNamingPolicy.CamelCase` |

Los JSON almacenados usan camelCase y sin indentado (optimizado para espacio). La deserializacion es case-insensitive.

---

## Testing

### Tests de integracion (Azurite)

Los tests del proyecto `Akay.To.Azure.Tests` requieren **Azurite** ejecutandose localmente.

```powershell
# Iniciar Azurite (Docker)
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite

# Ejecutar tests
dotnet test Akay.To.Azure.Tests.csproj --configuration Release
```

### Mock de IBlobStorageService

Para tests unitarios de consumidores del servicio:

```csharp
var mockBlob = new Mock<IBlobStorageService>();

mockBlob.Setup(b => b.SetContainerAsync(
        "test", false, null, false, It.IsAny<CancellationToken>()))
    .Returns(Task.CompletedTask);

mockBlob.Setup(b => b.UploadAsync(
        "key", It.IsAny<MyPayload>(), false, false, It.IsAny<CancellationToken>()))
    .ReturnsAsync("https://127.0.0.1:10000/devstoreaccount1/test/key");

mockBlob.Setup(b => b.DownloadAsync<MyPayload>(
        "key", false, It.IsAny<CancellationToken>()))
    .ReturnsAsync(new MyPayload { Id = 1 });

mockBlob.Setup(b => b.ExistsAsync("key", It.IsAny<CancellationToken>()))
    .ReturnsAsync(true);

mockBlob.Setup(b => b.DeleteAsync("key", It.IsAny<CancellationToken>()))
    .ReturnsAsync(true);

mockBlob.Setup(b => b.GetMetadataAsync("key", It.IsAny<CancellationToken>()))
    .ReturnsAsync(new Dictionary<string, string> { ["version"] = "1.0" });

mockBlob.Setup(b => b.BlobsNameAsync("prefix/", It.IsAny<CancellationToken>()))
    .ReturnsAsync(["prefix/file1.json", "prefix/file2.json"]);

mockBlob.Setup(b => b.GenerateReadSasUri("key", It.IsAny<DateTimeOffset?>()))
    .Returns("https://account.blob.core.windows.net/container/key?sas_token");
```
