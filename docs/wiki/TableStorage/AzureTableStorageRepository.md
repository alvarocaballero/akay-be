# Azure Table Storage Repository

## Que es

`ITableStorageRepository` es una abstraccion sobre `Azure.Data.Tables` que ofrece operaciones CRUD sobre tablas de Azure Storage, lectura/escritura de objetos serializados via MemoArrays, consulta paginada, barrido de claves y verificacion de existencia, con un modelo de errores basado en `Result<T>`.

Cada instancia del repositorio esta ligada a una tabla concreta y se obtiene via factory. La interfaz `ITableStorageExtendedRepository` (interna de `Akay.To.Azure`) extiende la base con metodos que exponen `ITableEntity`.

**Paquete:** `Akay.To.Azure`
**Interfaz (Core):** `Akay.To.Core.Application.Abstractions.TableStorage.ITableStorageRepository`
**Factory (Core):** `Akay.To.Core.Application.Abstractions.TableStorage.ITableStorageRepositoryFactory`
**Interfaz extendida (Azure):** `Akay.To.Azure.Infrastructure.Abstractions.ITableStorageExtendedRepository`
**Implementacion:** `Akay.To.Azure.Infrastructure.Repositories.AzureTableStorageRepository`
**Registro DI:** `Akay.To.Azure.Infrastructure.DependencyInjection.AzureTableStorageConfiguration`

---

## Por que usarlo

- **Abstraccion desacoplada:** `Akay.To.Core` y las APIs consumidoras no referencian `Azure.Data.Tables`. Solo dependen de `ITableStorageRepository` y sus contratos.
- **Result<T> consistente:** todos los metodos retornan `Result` o `Result<T>`, eliminando nulos silenciosos y propagando errores tipados (`NotFound`, `Conflict`, `Timeout`, etc.).
- **MemoArrays para objetos serializados:** permite almacenar cualquier objeto CLR serializado como JSON junto con su tipo (`AssemblyQualifiedName`), y recuperarlo tanto con tipo conocido (`GetObjectAsync<T>`) como con resolucion dinamica (`GetObjectAsync`).
- **Filtros OData seguros:** `TableStorageFilter` permite construir condiciones con `And`/`Or`/grupos sin escribir OData a mano y sin exponer `Azure.Data.Tables` en Core.
- **Control de creacion de tabla:** el parametro `forceCreateTable` (por defecto `false`) evita side effects en entornos con permisos limitados.
- **Paginacion homogenea:** `QueryAsync` y `GetPaginatedEntitiesAsync` devuelven `PaginatedResponse<T>` con `Page`/`NextPage` en `string` (usable tanto con numeros de pagina como con `continuationToken`).
- **Delete idempotente:** `DeleteEntityAsync` trata HTTP 404 como exito, evitando excepciones por entidades ya eliminadas.
- **Errores mapeados a HTTP:** `MapStorageError` traduce los codigos de estado de Azure a tipos de `Error` semanticos (`Unauthorized`, `Forbidden`, `NotFound`, `Conflict`, `Timeout`, `Unavailable`).
- **Health check sin side effects:** usa `TableServiceClient.GetPropertiesAsync()`, sin crear tablas.
- **Dos niveles de API:** `ITableStorageRepository` (Core, sin `Azure.Data.Tables`) y `ITableStorageExtendedRepository` (Azure, con `ITableEntity`) permiten elegir el nivel de acoplamiento.

---

## Arquitectura

### ITableStorageRepositoryFactory

```csharp
public interface ITableStorageRepositoryFactory
{
    ITableStorageRepository Create(string tableName, bool forceCreateTable = false);
}
```

La factory es stateless y devuelve una instancia ya inicializada ligada a la tabla indicada.

### ITableStorageRepository (contrato completo)

```csharp
public interface ITableStorageRepository
{
    #region Query
    Task<Result<(List<TEntity> Results, string? NextToken)>> QueryAsync<TEntity>(
            string filter, int pageSize,
            string? continuationToken = null,
            CancellationToken cancellationToken = default) where TEntity : class, new();

    Task<Result<(List<TEntity> Results, string? NextToken)>> QueryAsync<TEntity>(
            TableStorageFilter filter, int pageSize,
            string? continuationToken = null,
            CancellationToken cancellationToken = default) where TEntity : class, new();
    #endregion

    #region GetObjects
    Task<Result<IEnumerable<TType>>> GetObjectsByPartitionKeyAsync<TType>(
            string partitionKey, CancellationToken cancellationToken = default) where TType : class;
    Task<Result<IEnumerable<TType>>> GetObjectsByRowKeyAsync<TType>(
            string rowKey, CancellationToken cancellationToken = default) where TType : class;
    Task<Result<IEnumerable<TType>>> GetObjectsAsync<TType>(
            string filter, CancellationToken cancellationToken = default) where TType : class;
    Task<Result<IEnumerable<TType>>> GetObjectsAsync<TType>(
            TableStorageFilter filter, CancellationToken cancellationToken = default) where TType : class;
    #endregion

    #region GetMemoArrays
    Task<Result<List<MemoArray>>> GetMemoArraysByPartitionKeyAsync(
            string partitionKey, CancellationToken cancellationToken = default);
    Task<Result<List<MemoArray>>> GetMemoArraysByRowKeyAsync(
            string rowKey, CancellationToken cancellationToken = default);
    Task<Result<List<MemoArray>>> GetMemoArraysAsync(
            string filter, CancellationToken cancellationToken = default);
    Task<Result<List<MemoArray>>> GetMemoArraysAsync(
            TableStorageFilter filter, CancellationToken cancellationToken = default);
    #endregion

    #region GetKeys
    Task<Result<ICollection<(string PartitionKey, string RowKey)>>> GetKeysByPartitionKeyAsync(
            string partitionKey, CancellationToken cancellationToken = default);
    Task<Result<ICollection<(string PartitionKey, string RowKey)>>> GetKeysByRowKeyAsync(
            string rowKey, CancellationToken cancellationToken = default);
    Task<Result<ICollection<(string PartitionKey, string RowKey)>>> GetKeysAsync(
            string filter, CancellationToken cancellationToken = default);
    Task<Result<ICollection<(string PartitionKey, string RowKey)>>> GetKeysAsync(
            TableStorageFilter filter, CancellationToken cancellationToken = default);
    #endregion

    #region Get
    Task<Result<TType?>> GetAsync<TType>(
            string partitionKey, string rowKey,
            CancellationToken cancellationToken = default) where TType : class, new();
    #endregion

    #region GetObject
    Task<Result<object?>> GetObjectAsync(
            string partitionKey, string rowKey,
            CancellationToken cancellationToken = default);
    #endregion

    #region GetMemoArray
    Task<Result<MemoArray?>> GetMemoArrayAsync(
            string partitionKey, string rowKey,
            CancellationToken cancellationToken = default);
    #endregion

    #region Upsert
    Task<Result> UpsertAsync<TType>(
        string partitionKey, RowKeyType rowKey, TType values,
        UpdateMode updateMode = UpdateMode.Merge,
        CancellationToken cancellationToken = default) where TType : class;

    Task<Result> UpsertAsync<TType>(
        string partitionKey, string rowKey, TType values,
        UpdateMode updateMode = UpdateMode.Merge,
        CancellationToken cancellationToken = default) where TType : class;
    #endregion

    #region UpsertObject
    Task<Result> UpsertObjectAsync<TValue>(
            string partitionKey, RowKeyType rowKey, TValue? entity,
            UpdateMode updateMode = UpdateMode.Merge,
            CancellationToken cancellationToken = default) where TValue : class;

    Task<Result> UpsertObjectAsync<TValue>(
            string partitionKey, string rowKey, TValue? entity,
            UpdateMode updateMode = UpdateMode.Merge,
            CancellationToken cancellationToken = default) where TValue : class;
    #endregion

    #region Delete
    Task<Result> DeleteEntityAsync(
        string partitionKey, string rowKey,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteEntitiesAsync(
        string filter, CancellationToken cancellationToken = default);
    Task<Result> DeleteEntitiesAsync(
        TableStorageFilter filter, CancellationToken cancellationToken = default);

    Task<Result> DeleteEntitiesByPartitionKeyAsync(
        string partitionKey, CancellationToken cancellationToken = default);
    Task<Result> DeleteEntitiesByRowKeyAsync(
        string rowKey, CancellationToken cancellationToken = default);
    #endregion

    #region Exists
    Task<Result<bool>> ExistsAsync(
        string filter, CancellationToken cancellationToken = default);
    Task<Result<bool>> ExistsAsync(
        TableStorageFilter filter, CancellationToken cancellationToken = default);
    Task<Result<bool>> ExistsAsync(
        string partitionKey, string rowKey,
        CancellationToken cancellationToken = default);
    Task<Result<bool>> ExistsPartitionKeyAsync(
        string partitionKey, CancellationToken cancellationToken = default);
    Task<Result<bool>> ExistsRowKeyAsync(
        string rowKey, CancellationToken cancellationToken = default);
    #endregion
}
```

### ITableStorageExtendedRepository (solo para consumidores dentro de Akay.To.Azure)

```csharp
public interface ITableStorageExtendedRepositoryFactory
{
    ITableStorageExtendedRepository Create(string tableName, bool forceCreateTable = false);
}

public interface ITableStorageExtendedRepository : ITableStorageRepository
{
    Task<Result<IEnumerable<TEntity>>> GetEntitiesByPartitionKeyAsync<TEntity>(
        string partitionKey, CancellationToken cancellationToken = default)
        where TEntity : class, ITableEntity, new();

    Task<Result<IEnumerable<TEntity>>> GetEntitiesByRowKeyAsync<TEntity>(
        string rowKey, CancellationToken cancellationToken = default)
        where TEntity : class, ITableEntity, new();

    Task<Result<IEnumerable<TEntity>>> GetEntitiesAsync<TEntity>(
        string filter, CancellationToken cancellationToken = default)
        where TEntity : class, ITableEntity, new();
    Task<Result<IEnumerable<TEntity>>> GetEntitiesAsync<TEntity>(
        TableStorageFilter filter, CancellationToken cancellationToken = default)
        where TEntity : class, ITableEntity, new();

    Task<Result<PaginatedResponse<List<TEntity>>>> GetPaginatedEntitiesAsync<TEntity>(
        string filter, string? continuationToken = null, int? pageSize = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, ITableEntity, new();
    Task<Result<PaginatedResponse<List<TEntity>>>> GetPaginatedEntitiesAsync<TEntity>(
        TableStorageFilter filter, string? continuationToken = null, int? pageSize = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, ITableEntity, new();

    Task<Result<TEntity?>> GetEntityAsync<TEntity>(
        string partitionKey, string rowKey,
        CancellationToken cancellationToken = default)
        where TEntity : class, ITableEntity, new();

    Task<Result<TType?>> GetObjectAsync<TType>(
        string partitionKey, string rowKey,
        CancellationToken cancellationToken = default) where TType : class, new();

    Task<Result> UpsertEntityAsync<TEntity>(
        TEntity entity, UpdateMode updateMode = UpdateMode.Merge,
        CancellationToken cancellationToken = default)
        where TEntity : class, ITableEntity, new();
}
```

### Tipos auxiliares

```csharp
public enum UpdateMode
{
    Merge = 0,
    Replace = 1
}

public enum RowKeyType
{
    Ticks = 1,
    TicksDesc = 2,
    RandomGuid = 3
}

public record MemoArray(string? ObjectType, string? ObjectValue);

public record RecordLinks(string? Previous, string? Next);

public class PaginatedResponse<T>
{
    public T Data { get; private set; }
    public string? Page { get; private set; }
    public string? NextPage { get; private set; }
    public int? PageSize { get; private set; }
    public bool HasMoreItems { get; private set; }
    public RecordLinks Links { get; private set; }

    public static PaginatedResponse<T> Create(
        T data, int? pageSize, int page, bool hasMoreItems,
        string? previousLink = null, string? nextLink = null);

    public static PaginatedResponse<T> Create(
        T data, int? pageSize,
        string? continuationToken, string? nextContinuationToken,
        string? previousLink, string? nextLink);
}
```

### TableStorageFilter

```csharp
public enum TableStorageOperator
{
    Equal, NotEqual, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual
}

public enum TableStorageLogical { And, Or }

public sealed class TableStorageFilter
{
    public static TableStorageFilter Create();
    public static TableStorageFilter PartitionKey(string value);
    public static TableStorageFilter RowKey(string value);

    public TableStorageFilter And(string field, TableStorageOperator op, object? value);
    public TableStorageFilter Or(string field, TableStorageOperator op, object? value);
    public TableStorageFilter AndGroup(Func<TableStorageFilter, TableStorageFilter> build);
    public TableStorageFilter OrGroup(Func<TableStorageFilter, TableStorageFilter> build);
    public TableStorageFilter AndEqual(string field, object? value);
    public TableStorageFilter OrEqual(string field, object? value);
    public TableStorageFilter AndGreaterThanOrEqual(string field, object? value);
    public TableStorageFilter AndLessThan(string field, object? value);
}
```

---

## Configuracion

### Registro en DI

```csharp
using Akay.To.Azure.Infrastructure.DependencyInjection;

services.AddTableStorage(settings?.AzureStorageSettings);
```

### Que registra

| Servicio | Lifetime |
|---|---|
| `TableServiceClient` | Singleton |
| `ITableStorageRepositoryFactory` | Transient |
| `ITableStorageExtendedRepositoryFactory` | Transient |
| Health check `azure_tables` (tag: `table`) | Se anade al pipeline |

Si `AzureStorageSettings` es `null` o `ConnectionString` esta vacio, no se registra nada y el sistema opera sin table storage (fail-safe).

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

Los metodos se agrupan en: **Factory**, **Lectura de entidades**, **Lectura de objetos (MemoArrays)**, **Lectura de claves**, **Escritura**, **Eliminacion**, **Existencia**, **API extendida**.

---

### Factory

#### ITableStorageRepositoryFactory.Create

```csharp
ITableStorageRepository Create(string tableName, bool forceCreateTable = false);
```

| Parametro | Descripcion |
|---|---|
| `tableName` | Nombre de la tabla Azure. |
| `forceCreateTable` | Si es `true`, llama a `CreateIfNotExists()` en el momento de construir el repositorio. Por defecto `false`. |

La factory se resuelve via DI (`ITableStorageRepositoryFactory`) o directamente instanciando `TableStorageRepositoryFactory(TableServiceClient)`.

```csharp
// Desde DI en Akay.Be
var factory = serviceProvider.GetRequiredService<ITableStorageRepositoryFactory>();
var repo = factory.Create("customers");

// Con forceCreateTable: crea la tabla si no existe
var repo = factory.Create("orders", forceCreateTable: true);
```

---

### Lectura de entidades (via TableEntity)

#### GetAsync\<TType\>

Recupera una entidad por `PartitionKey` + `RowKey` y la convierte al tipo indicado via reflexion (`AzureTableHelper.FromTableEntity`).

```csharp
Task<Result<TType?>> GetAsync<TType>(
    string partitionKey, string rowKey,
    CancellationToken cancellationToken = default) where TType : class, new();
```

| Parametro | Descripcion |
|---|---|
| `partitionKey` | Partition key de la entidad. |
| `rowKey` | Row key de la entidad. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<TType?>` — exito con la entidad convertida, o `null` si no tiene `ObjectValue`. `Failure` con `Error.NotFound` si la entidad no existe o la conversion falla. |

```csharp
var result = await repo.GetAsync<CustomerEntity>("cust-123", "row-1", ct);
if (result.TryGetValue(out var customer) && customer is not null)
    Console.WriteLine(customer.Name);
```

#### QueryAsync\<TEntity\>

Consulta paginada con filtro OData (string o `TableStorageFilter`). Convierte cada `TableEntity` al tipo `TEntity` via reflexion.

```csharp
// Sobrecarga string filter
Task<Result<(List<TEntity> Results, string? NextToken)>> QueryAsync<TEntity>(
    string filter, int pageSize,
    string? continuationToken = null,
    CancellationToken cancellationToken = default) where TEntity : class, new();

// Sobrecarga TableStorageFilter
Task<Result<(List<TEntity> Results, string? NextToken)>> QueryAsync<TEntity>(
    TableStorageFilter filter, int pageSize,
    string? continuationToken = null,
    CancellationToken cancellationToken = default) where TEntity : class, new();
```

| Parametro | Descripcion |
|---|---|
| `filter` | Filtro OData (`string`) o `TableStorageFilter`. |
| `pageSize` | Numero maximo de entidades por pagina. |
| `continuationToken` | Token de continuacion de Azure (opcional, para paginar). |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<(List<TEntity> Results, string? NextToken)>` — exito con lista de entidades y token de siguiente pagina. `Failure` si el filtro OData es invalido o hay error de Azure. |

```csharp
var filter = TableStorageFilter
    .PartitionKey("cust-123")
    .AndEqual("Status", "Active");

var result = await repo.QueryAsync<CustomerEntity>(filter, pageSize: 50, ct);

if (result.TryGetValue(out var page))
{
    foreach (var customer in page.Results)
        Console.WriteLine(customer.Name);

    // Paginar siguiente pagina
    if (page.NextToken is not null)
    {
        var nextPage = await repo.QueryAsync<CustomerEntity>(
            filter, pageSize: 50, continuationToken: page.NextToken, ct);
    }
}
```

---

### Lectura de objetos (MemoArrays)

Los "MemoArrays" son el mecanismo para almacenar objetos CLR serializados como JSON en una tabla Azure, guardando tambien el tipo (`AssemblyQualifiedName`) para poder deserializarlos dinamicamente.

#### GetObjectsAsync\<TType\>

Obtiene objetos deserializados desde MemoArrays, aplicando un filtro OData (string o `TableStorageFilter`). Si `TType` es `string`, devuelve los valores crudos sin deserializar.

```csharp
// Sobrecarga string filter
Task<Result<IEnumerable<TType>>> GetObjectsAsync<TType>(
    string filter, CancellationToken cancellationToken = default) where TType : class;

// Sobrecarga TableStorageFilter
Task<Result<IEnumerable<TType>>> GetObjectsAsync<TType>(
    TableStorageFilter filter, CancellationToken cancellationToken = default) where TType : class;

// Sobrecarga por PartitionKey
Task<Result<IEnumerable<TType>>> GetObjectsByPartitionKeyAsync<TType>(
    string partitionKey, CancellationToken cancellationToken = default) where TType : class;

// Sobrecarga por RowKey
Task<Result<IEnumerable<TType>>> GetObjectsByRowKeyAsync<TType>(
    string rowKey, CancellationToken cancellationToken = default) where TType : class;
```

| Parametro | Descripcion |
|---|---|
| `filter` | Filtro OData (`string`) o `TableStorageFilter`. |
| `partitionKey` | Filtro por partition key exacta. |
| `rowKey` | Filtro por row key exacta. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<IEnumerable<TType>>` — exito con los objetos deserializados. `Failure` si ocurre error de Azure, `ObjectValue` es null/vacio, o falla la deserializacion JSON. |

```csharp
var filter = TableStorageFilter
    .PartitionKey("user-abc")
    .AndGreaterThanOrEqual("CreatedAt", DateTimeOffset.UtcNow.AddDays(-7));

var result = await repo.GetObjectsAsync<UserProfile>(filter, ct);

if (result.TryGetValue(out var profiles))
{
    foreach (var p in profiles)
        Console.WriteLine($"{p.Name} - {p.Email}");
}
```

#### GetObjectAsync\<TType\> (Extended)

Recupera un unico objeto serializado por `PartitionKey` + `RowKey`.

```csharp
Task<Result<TType?>> GetObjectAsync<TType>(
    string partitionKey, string rowKey,
    CancellationToken cancellationToken = default) where TType : class, new();
```

| Parametro | Descripcion |
|---|---|
| `partitionKey` | Partition key. |
| `rowKey` | Row key. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<TType?>` — exito con el objeto deserializado, o `Success(null)` si no tiene `ObjectValue`. `Failure` si falla la deserializacion. |

#### GetObjectAsync (no generico)

Recupera un objeto serializado resolviendo el tipo dinamicamente via `AssemblyQualifiedName` almacenado.

```csharp
Task<Result<object?>> GetObjectAsync(
    string partitionKey, string rowKey,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `partitionKey` | Partition key. |
| `rowKey` | Row key. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<object?>` — exito con el objeto deserializado usando el tipo resuelto. `Failure(NotFound)` si el tipo no se puede resolver. `Success(null)` si no hay datos. |

#### GetMemoArraysAsync

Obtiene los MemoArrays crudos (sin deserializar) aplicando un filtro.

```csharp
Task<Result<List<MemoArray>>> GetMemoArraysAsync(
    string filter, CancellationToken cancellationToken = default);
Task<Result<List<MemoArray>>> GetMemoArraysAsync(
    TableStorageFilter filter, CancellationToken cancellationToken = default);
Task<Result<List<MemoArray>>> GetMemoArraysByPartitionKeyAsync(
    string partitionKey, CancellationToken cancellationToken = default);
Task<Result<List<MemoArray>>> GetMemoArraysByRowKeyAsync(
    string rowKey, CancellationToken cancellationToken = default);
```

#### GetMemoArrayAsync

Obtiene un unico MemoArray por `PartitionKey` + `RowKey`.

```csharp
Task<Result<MemoArray?>> GetMemoArrayAsync(
    string partitionKey, string rowKey,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `partitionKey` | Partition key. |
| `rowKey` | Row key. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result<MemoArray?>` — exito con el MemoArray (contiene `ObjectType` y `ObjectValue`). `Success(null)` si no tiene `ObjectValue`. `Failure` si error de Azure. |

```csharp
var result = await repo.GetMemoArrayAsync("tenant-1", "config:app", ct);

if (result.TryGetValue(out var memo) && memo is not null)
{
    Console.WriteLine($"Type: {memo.ObjectType}");
    Console.WriteLine($"Value: {memo.ObjectValue}");
}
```

---

### Lectura de claves

#### GetKeysAsync

Obtiene las claves (`PartitionKey`, `RowKey`) de las entidades que coinciden con un filtro. Util para escaneos ligeros sin descargar el contenido completo.

```csharp
Task<Result<ICollection<(string PartitionKey, string RowKey)>>> GetKeysAsync(
    string filter, CancellationToken cancellationToken = default);
Task<Result<ICollection<(string PartitionKey, string RowKey)>>> GetKeysAsync(
    TableStorageFilter filter, CancellationToken cancellationToken = default);
Task<Result<ICollection<(string PartitionKey, string RowKey)>>> GetKeysByPartitionKeyAsync(
    string partitionKey, CancellationToken cancellationToken = default);
Task<Result<ICollection<(string PartitionKey, string RowKey)>>> GetKeysByRowKeyAsync(
    string rowKey, CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `filter` | Filtro OData o `TableStorageFilter`. |
| `partitionKey` | Filtro por partition key exacta. |
| `rowKey` | Filtro por row key exacta. |
| **Retorna** | `Result<ICollection<(string, string)>>` — exito con coleccion de tuplas `(PartitionKey, RowKey)`. |

```csharp
var result = await repo.GetKeysByPartitionKeyAsync("tenant-1", ct);

if (result.TryGetValue(out var keys))
{
    foreach (var (pk, rk) in keys)
        Console.WriteLine($"PK={pk}, RK={rk}");
}
```

---

### Escritura

#### UpsertAsync\<TType\>

Inserta o actualiza (merge o replace) una entidad convirtiendo un objeto CLR a `TableEntity` via reflexion.

```csharp
// Con generacion automatica de RowKey
Task<Result> UpsertAsync<TType>(
    string partitionKey, RowKeyType rowKey, TType values,
    UpdateMode updateMode = UpdateMode.Merge,
    CancellationToken cancellationToken = default) where TType : class;

// Con RowKey explicita
Task<Result> UpsertAsync<TType>(
    string partitionKey, string rowKey, TType values,
    UpdateMode updateMode = UpdateMode.Merge,
    CancellationToken cancellationToken = default) where TType : class;
```

| Parametro | Descripcion |
|---|---|
| `partitionKey` | Partition key. |
| `rowKey` | `string` explicita o `RowKeyType` (genera automaticamente `Ticks`, `TicksDesc` o `RandomGuid`). |
| `values` | Objeto a serializar como propiedades de `TableEntity`. Si es `null`, el metodo retorna `Success` sin hacer nada. |
| `updateMode` | `Merge` (por defecto) o `Replace`. |
| `cancellationToken` | Token de cancelacion. |
| **Retorna** | `Result` — `Success` si la operacion se completo, `Failure` si fallo la conversion a `TableEntity` o la operacion Azure. |

```csharp
var customer = new CustomerEntity { Name = "Alice", Status = "Active" };

// Con RowKey aleatoria (GUID)
var result = await repo.UpsertAsync("cust-123", RowKeyType.RandomGuid, customer, ct);

// Con RowKey explicita
var result = await repo.UpsertAsync("cust-123", "profile", customer, ct);

// Con Replace en lugar de Merge
var result = await repo.UpsertAsync("cust-123", "profile", customer, UpdateMode.Replace, ct);

if (result.IsFailure)
    Console.WriteLine($"Error: {result.Error}");
```

#### UpsertObjectAsync\<TValue\>

Inserta o actualiza un objeto serializado como MemoArray (JSON + `AssemblyQualifiedName`). Es el mecanismo para persistir objetos arbitrarios en una tabla Azure.

```csharp
// Con generacion automatica de RowKey
Task<Result> UpsertObjectAsync<TValue>(
    string partitionKey, RowKeyType rowKey, TValue? entity,
    UpdateMode updateMode = UpdateMode.Merge,
    CancellationToken cancellationToken = default) where TValue : class;

// Con RowKey explicita
Task<Result> UpsertObjectAsync<TValue>(
    string partitionKey, string rowKey, TValue? entity,
    UpdateMode updateMode = UpdateMode.Merge,
    CancellationToken cancellationToken = default) where TValue : class;
```

| Parametro | Descripcion |
|---|---|
| `partitionKey` | Partition key. |
| `rowKey` | `string` explicita o `RowKeyType`. |
| `entity` | Objeto a serializar como JSON. Si es `null`, se serializa como `null`. El tipo se guarda usando `entity.GetType().AssemblyQualifiedName` (fallback a `typeof(TValue).AssemblyQualifiedName`). |
| `updateMode` | `Merge` o `Replace`. |
| **Retorna** | `Result` — `Success` si se completo, `Failure` si fallo Azure. |

```csharp
var profile = new UserProfile { Id = Guid.NewGuid(), Name = "Bob", Roles = ["admin"] };

// Guardar como MemoArray
var result = await repo.UpsertObjectAsync("users", profile.Id.ToString(), profile, ct);

// Recuperar despues
var restored = await repo.GetObjectAsync<UserProfile>("users", profile.Id.ToString(), ct);
```

---

### Eliminacion

#### DeleteEntityAsync

Elimina una entidad por `PartitionKey` + `RowKey`. **Idempotente:** si la entidad no existe (404), retorna `Success`.

```csharp
Task<Result> DeleteEntityAsync(
    string partitionKey, string rowKey,
    CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `partitionKey` | Partition key. |
| `rowKey` | Row key. |
| **Retorna** | `Result` — `Success` si se elimino o no existia. `Failure` si error de Azure distinto de 404. |

```csharp
var result = await repo.DeleteEntityAsync("cust-123", "profile", ct);
// Success incluso si no existia
```

#### DeleteEntitiesAsync

Elimina todas las entidades que coinciden con un filtro OData o `TableStorageFilter`. Itera las paginas y elimina secuencialmente.

```csharp
Task<Result> DeleteEntitiesAsync(
    string filter, CancellationToken cancellationToken = default);
Task<Result> DeleteEntitiesAsync(
    TableStorageFilter filter, CancellationToken cancellationToken = default);
Task<Result> DeleteEntitiesByPartitionKeyAsync(
    string partitionKey, CancellationToken cancellationToken = default);
Task<Result> DeleteEntitiesByRowKeyAsync(
    string rowKey, CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `filter` | Filtro OData o `TableStorageFilter`. |
| `partitionKey` | Elimina todas las entidades con esa partition key. |
| `rowKey` | Elimina todas las entidades con esa row key. |
| **Retorna** | `Result` — `Success` si se eliminaron todas (o ninguna). `Failure` si error de Azure distinto de 404. |

```csharp
// Eliminar todo de una particion
await repo.DeleteEntitiesByPartitionKeyAsync("tenant-1", ct);

// Eliminar con filtro
var filter = TableStorageFilter.PartitionKey("tenant-1").AndEqual("Status", "Expired");
await repo.DeleteEntitiesAsync(filter, ct);
```

---

### Existencia

#### ExistsAsync

Verifica si existe al menos una entidad que coincida con el filtro, o una entidad concreta por `PartitionKey` + `RowKey`.

```csharp
// Por filtro
Task<Result<bool>> ExistsAsync(string filter, CancellationToken cancellationToken = default);
Task<Result<bool>> ExistsAsync(TableStorageFilter filter, CancellationToken cancellationToken = default);

// Por clave compuesta
Task<Result<bool>> ExistsAsync(
    string partitionKey, string rowKey, CancellationToken cancellationToken = default);

// Por particion
Task<Result<bool>> ExistsPartitionKeyAsync(
    string partitionKey, CancellationToken cancellationToken = default);

// Por row key
Task<Result<bool>> ExistsRowKeyAsync(
    string rowKey, CancellationToken cancellationToken = default);
```

| Parametro | Descripcion |
|---|---|
| `filter` | Filtro OData o `TableStorageFilter`. |
| `partitionKey` | Partition key. |
| `rowKey` | Row key. |
| **Retorna** | `Result<bool>` — `Success(true)` si existe, `Success(false)` si no. `Failure` si error de Azure. |

```csharp
var existsResult = await repo.ExistsAsync("cust-123", "profile", ct);

if (existsResult.TryGetValue(out var exists) && exists)
    Console.WriteLine("La entidad existe");
```

---

### API extendida (ITableStorageExtendedRepository)

Estos metodos requieren `ITableEntity` y solo estan disponibles via `ITableStorageExtendedRepository` (consumidores dentro de `Akay.To.Azure`).

#### GetEntitiesAsync\<TEntity\>

Igual que `GetObjectsAsync` pero trabajando directamente con entidades `ITableEntity` (sin serializacion JSON intermedia).

```csharp
Task<Result<IEnumerable<TEntity>>> GetEntitiesAsync<TEntity>(
    string filter, CancellationToken cancellationToken = default)
    where TEntity : class, ITableEntity, new();
Task<Result<IEnumerable<TEntity>>> GetEntitiesAsync<TEntity>(
    TableStorageFilter filter, CancellationToken cancellationToken = default)
    where TEntity : class, ITableEntity, new();
```

```csharp
// Desde dentro de Akay.To.Azure
var extendedRepo = (ITableStorageExtendedRepository)repo;
var result = await extendedRepo.GetEntitiesAsync<OrderEntity>(
    $"PartitionKey eq 'cust-123'", ct);
```

#### GetEntityAsync\<TEntity\>

Recupera una entidad `ITableEntity` por `PartitionKey` + `RowKey`.

```csharp
Task<Result<TEntity?>> GetEntityAsync<TEntity>(
    string partitionKey, string rowKey,
    CancellationToken cancellationToken = default)
    where TEntity : class, ITableEntity, new();
```

#### GetPaginatedEntitiesAsync\<TEntity\>

Version paginada de `GetEntitiesAsync` que devuelve `PaginatedResponse<List<TEntity>>` con `continuationToken`.

```csharp
Task<Result<PaginatedResponse<List<TEntity>>>> GetPaginatedEntitiesAsync<TEntity>(
    string filter, string? continuationToken = null, int? pageSize = null,
    CancellationToken cancellationToken = default) where TEntity : class, ITableEntity, new();
Task<Result<PaginatedResponse<List<TEntity>>>> GetPaginatedEntitiesAsync<TEntity>(
    TableStorageFilter filter, string? continuationToken = null, int? pageSize = null,
    CancellationToken cancellationToken = default) where TEntity : class, ITableEntity, new();
```

| Parametro | Descripcion |
|---|---|
| `filter` | Filtro OData o `TableStorageFilter`. |
| `continuationToken` | Token de continuacion (opcional). |
| `pageSize` | Numero maximo de entidades por pagina. Si no se especifica, Azure usa su default. |
| **Retorna** | `Result<PaginatedResponse<List<TEntity>>>` con las entidades de la pagina actual y el token de siguiente pagina en `NextPage`. |

```csharp
var filter = TableStorageFilter.PartitionKey("orders-2026");

var pageResult = await extendedRepo.GetPaginatedEntitiesAsync<OrderEntity>(
    filter, pageSize: 25, ct);

if (pageResult.TryGetValue(out var page))
{
    ProcessOrders(page.Data);

    while (page.HasMoreItems && page.NextPage is not null)
    {
        pageResult = await extendedRepo.GetPaginatedEntitiesAsync<OrderEntity>(
            filter, continuationToken: page.NextPage, pageSize: 25, ct);
        if (pageResult.TryGetValue(out page))
            ProcessOrders(page.Data);
    }
}
```

#### UpsertEntityAsync\<TEntity\>

Inserta o actualiza una entidad `ITableEntity` directamente (sin conversion via reflexion).

```csharp
Task<Result> UpsertEntityAsync<TEntity>(
    TEntity entity,
    UpdateMode updateMode = UpdateMode.Merge,
    CancellationToken cancellationToken = default)
    where TEntity : class, ITableEntity, new();
```

```csharp
var orderEntity = new OrderEntity
{
    PartitionKey = "orders-2026",
    RowKey = Guid.NewGuid().ToString(),
    Amount = 99.99m,
    Status = "Pending"
};

var result = await extendedRepo.UpsertEntityAsync(orderEntity, ct);
```

---

## Implementacion interna

### Factories

```csharp
public class TableStorageRepositoryFactory(TableServiceClient serviceClient)
    : ITableStorageRepositoryFactory
{
    public ITableStorageRepository Create(string tableName, bool forceCreateTable = false) =>
        new AzureTableStorageRepository(serviceClient, tableName, forceCreateTable);
}

public class TableStorageExtendedRepositoryFactory(TableServiceClient serviceClient)
    : ITableStorageExtendedRepositoryFactory
{
    public ITableStorageExtendedRepository Create(string tableName, bool forceCreateTable = false) =>
        new AzureTableStorageRepository(serviceClient, tableName, forceCreateTable);
}
```

Ambas factories son stateless: reciben el `TableServiceClient` (singleton via DI) y construyen una nueva instancia de `AzureTableStorageRepository` por tabla.

### Constructor del repositorio

```csharp
public AzureTableStorageRepository(
    TableServiceClient serviceClient, string tableName, bool forceCreateTable = false)
{
    _tableClient = serviceClient.GetTableClient(tableName);

    if (forceCreateTable)
        _tableClient.CreateIfNotExists();
}
```

El constructor resuelve el `TableClient` para la tabla indicada. Por defecto (`forceCreateTable = false`) **no** crea la tabla; solo la crea si se solicita explicitamente.

### Metodos privados clave

| Metodo | Funcion |
|---|---|
| `MapStorageError(RequestFailedException, string)` | Traduce codigos HTTP de Azure a `Error` semanticos (ver tabla abajo). |

### Mapeo de errores de Azure

| HTTP Status | Error Type |
|---|---|
| 401 | `Error.Unauthorized` |
| 403 | `Error.Forbidden` |
| 404 | `Error.NotFound` |
| 408 | `Error.Timeout` |
| 409 | `Error.Conflict` |
| 503 | `Error.Unavailable` |
| Otros | `Error.Failure` |

```csharp
private static Error MapStorageError(RequestFailedException ex, string code) =>
    ex.Status switch
    {
        401 => Error.Unauthorized($"{code}.unauthorized", ex.Message),
        403 => Error.Forbidden($"{code}.forbidden", ex.Message),
        404 => Error.NotFound($"{code}.not_found", ex.Message),
        408 => Error.Timeout($"{code}.timeout", ex.Message),
        409 => Error.Conflict($"{code}.conflict", ex.Message),
        503 => Error.Unavailable($"{code}.unavailable", ex.Message),
        _ => Error.Failure($"{code}.failure", ex.Message)
    };
```

### Helpers internos

#### AzureTableHelper

Convierte entre objetos CLR y `TableEntity` (y viceversa) usando reflexion. Ambos metodos retornan `Result` para propagar errores de conversion en lugar de silenciarlos.

- `ToTableEntity<TType>`: serializa propiedades publicas de `TType` a un `TableEntity`. Si `values` es `null`, retorna `Success(null)` (no-op). Si falla la reflexion, retorna `Failure(Error.Internal)`.
- `FromTableEntity<TModel>`: hidrata un objeto `TModel` desde un `TableEntity`. Si `entity` es `null`, retorna `Success(null)`. Si falla la reflexion, retorna `Failure(Error.Internal)`.

```csharp
public static Result<TableEntity?> ToTableEntity<TType>(
    string partitionKey, string rowKey, TType? values) where TType : class;
public static Result<TModel?> FromTableEntity<TModel>(
    TableEntity? entity) where TModel : class, new();
```

#### TableStorageFilterODataBuilder

Traduce un `TableStorageFilter` a un string OData escapando correctamente los valores segun su tipo (`string`, `DateTime`, `DateTimeOffset`, `bool`, `Guid`, `enum`, `IFormattable`).

```csharp
internal static class TableStorageFilterODataBuilder
{
    public static string Build(TableStorageFilter filter);
}
```

### Health check

```csharp
internal class TableStorageHealthCheck(TableServiceClient tableServiceClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await tableServiceClient.GetPropertiesAsync(cancellationToken);
            return HealthCheckResult.Healthy("Azure Table Storage esta disponible.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Error en Azure Table Storage.", ex);
        }
    }
}
```

- Es `internal`: no se expone fuera de `Akay.To.Azure`.
- Usa `TableServiceClient.GetPropertiesAsync()`: **no crea tablas**.
- Se registra con nombre `azure_tables` y tag `table`.
- Si `ConnectionString` esta vacio, el health check no se registra.

---

## Seccion especifica del dominio: MemoArrays

Los MemoArrays son el mecanismo para persistir objetos CLR arbitrarios en una tabla Azure sin necesidad de definir entidades `ITableEntity` para cada tipo.

### Como funciona

| Operacion | Que guarda |
|---|---|
| `UpsertObjectAsync` | `ObjectType = entity.GetType().AssemblyQualifiedName`, `ObjectValue = JsonSerializer.Serialize(entity)` |
| `GetObjectAsync<TType>` | Deserializa `ObjectValue` como `TType` |
| `GetObjectAsync` (no generico) | Resuelve `ObjectType` via `Type.GetType()` y deserializa con `JsonSerializer.Deserialize(value, resolvedType)` |

### Prioridad de resolucion de tipo

1. `entity?.GetType().AssemblyQualifiedName` (tipo runtime del objeto pasado a `UpsertObjectAsync`)
2. `typeof(TValue).AssemblyQualifiedName` (tipo generico en tiempo de compilacion)
3. `string.Empty` (fallback, resultara en error al recuperar)

### Comportamiento al recuperar

- Si `ObjectValue` es `null`: `GetObjectAsync<T>` retorna `Success(null)`. `GetObjectAsync` no generico retorna `Success(null)`.
- Si el tipo no se puede resolver (`Type.GetType` retorna `null`): `GetObjectAsync` no generico retorna `Failure(Error.NotFound)`.
- Si la deserializacion JSON falla: retorna `Failure(Error.Internal)`.

---

## Health Check

El health check `azure_tables` (tag: `table`) se registra automaticamente al llamar a `AddTableStorage()`.

```csharp
// Se registra automaticamente:
services.AddTableStorage(settings?.AzureStorageSettings);

// El endpoint de health checks incluira:
// GET /healthz --> "azure_tables": Healthy/Unhealthy
```

- Verifica conectividad llamando a `TableServiceClient.GetPropertiesAsync()`.
- No crea tablas ni entidades.
- Si `ConnectionString` esta vacio, no se registra.

---

## Consideraciones

### Idempotencia en eliminacion

`DeleteEntityAsync` trata HTTP 404 como exito. `DeleteEntitiesAsync` tambien captura 404 de Azure y retorna `Success`. No es necesario verificar existencia antes de eliminar.

### Validacion de entrada

Los metodos que reciben `partitionKey` o `rowKey` no validan nulos/vacios a nivel de interfaz. La validacion ocurre implicitamente en el SDK de Azure, que lanzara `RequestFailedException` capturada y convertida a `Result.Failure`.

### Modos de actualizacion

| Modo | Comportamiento |
|---|---|
| `Merge` (por defecto) | Solo actualiza las propiedades enviadas; el resto se mantiene. |
| `Replace` | Reemplaza toda la entidad; propiedades no enviadas se pierden. |

### forceCreateTable

Por defecto, `Create(tableName)` **no** crea la tabla. Si la tabla no existe, las operaciones fallaran con `Result.Failure(Error.NotFound(...))`. Usa `Create(tableName, forceCreateTable: true)` cuando necesites crear la tabla bajo demanda.

### Serializacion de tipos

Los MemoArrays usan `System.Text.Json` con defaults (sin opciones especiales). Para tipos con `JsonPropertyName` o configuraciones especificas, la serializacion/deserializacion puede no coincidir si no se usan los mismos settings en escritura y lectura.

### Limitaciones de OData

Los metodos que aceptan `TableStorageFilter` generan OData internamente. Sin embargo, Azure Table Storage tiene restricciones en OData:
- Maximo 15 condiciones combinadas por query.
- No soporta funciones como `startswith`, `contains`, `substring`.
- `RowKey` y `PartitionKey` son `string` en OData, independientemente del tipo de dato que almacenen.

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

### Mock de ITableStorageRepository

Para tests unitarios de consumidores del repositorio:

```csharp
var mockRepo = new Mock<ITableStorageRepository>();

// GetAsync
mockRepo.Setup(r => r.GetAsync<CustomerEntity>(
        "cust-123", "row-1", It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<CustomerEntity?>.Success(
        new CustomerEntity { Name = "Alice" }));

// GetObjectsAsync
mockRepo.Setup(r => r.GetObjectsAsync<UserProfile>(
        It.IsAny<TableStorageFilter>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<IEnumerable<UserProfile>>.Success(
        new[] { new UserProfile { Name = "Bob" } }));

// UpsertAsync
mockRepo.Setup(r => r.UpsertAsync(
        "cust-123", It.IsAny<string>(), It.IsAny<CustomerEntity>(),
        It.IsAny<UpdateMode>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result.Success());

// DeleteEntityAsync
mockRepo.Setup(r => r.DeleteEntityAsync(
        "cust-123", "row-1", It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result.Success());

// ExistsAsync
mockRepo.Setup(r => r.ExistsAsync(
        "cust-123", "row-1", It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<bool>.Success(true));

// QueryAsync
mockRepo.Setup(r => r.QueryAsync<CustomerEntity>(
        It.IsAny<TableStorageFilter>(), 50, null, It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<(List<CustomerEntity>, string?)>.Success((
        new List<CustomerEntity> { new() { Name = "Alice" } },
        "next-token")));

// GetMemoArrayAsync
mockRepo.Setup(r => r.GetMemoArrayAsync(
        "tenant-1", "config", It.IsAny<CancellationToken>()))
    .ReturnsAsync(Result<MemoArray?>.Success(
        new MemoArray("MyApp.Config", "{\"key\":\"value\"}")));
```

---

## Ejemplos de uso

### Desde Akay.Be / APIs consumidoras (ITableStorageRepository)

#### 1. Obtener repositorio desde la factory

```csharp
var factory = serviceProvider.GetRequiredService<ITableStorageRepositoryFactory>();
var repo = factory.Create("customers", forceCreateTable: true);
```

#### 2. Upsert de una entidad

```csharp
var customer = new CustomerEntity
{
    Name = "Alice",
    Email = "alice@example.com",
    Status = "Active"
};

var result = await repo.UpsertAsync("tenant-1", RowKeyType.RandomGuid, customer, ct);

if (result.TryGetError(out var error))
    _logger.LogError("Error guardando cliente: {Error}", error);
```

#### 3. Upsert de un objeto arbitrario (MemoArray)

```csharp
var settings = new TenantSettings
{
    MaxUsers = 100,
    Features = ["reports", "export"],
    Theme = "dark"
};

await repo.UpsertObjectAsync("tenant-1", "settings", settings, ct);
```

#### 4. Obtener objetos con filtro simple (PartitionKey)

```csharp
var result = await repo.GetObjectsByPartitionKeyAsync<CustomerEntity>("tenant-1", ct);

if (result.TryGetValue(out var customers))
{
    foreach (var c in customers)
        Console.WriteLine($"{c.Name} ({c.Status})");
}
```

#### 5. Obtener objetos con filtro And/Or

```csharp
var filter = TableStorageFilter
    .PartitionKey("tenant-1")
    .AndGroup(g => g
        .AndEqual("Status", "Active")
        .OrEqual("Status", "Pending"));

var result = await repo.GetObjectsAsync<CustomerEntity>(filter, ct);
```

#### 6. Obtener objetos con rango de fechas

```csharp
var filter = TableStorageFilter
    .PartitionKey("tenant-1")
    .AndGroup(g => g
        .AndEqual("Status", "Active")
        .OrEqual("Status", "Pending"))
    .AndGreaterThanOrEqual("CreatedAt", DateTimeOffset.UtcNow.AddDays(-30))
    .AndLessThan("CreatedAt", DateTimeOffset.UtcNow);

var result = await repo.GetObjectsAsync<EventDto>(filter, ct);
```

#### 7. Verificar existencia

```csharp
var exists = await repo.ExistsPartitionKeyAsync("tenant-1", ct);

if (exists.TryGetValue(out var hasData) && hasData)
    Console.WriteLine("La particion tiene datos");
```

#### 8. Eliminar por filtro

```csharp
var filter = TableStorageFilter
    .PartitionKey("tenant-1")
    .AndEqual("Status", "Expired");

await repo.DeleteEntitiesAsync(filter, ct);
```

#### 9. Usar escape hatch con string OData

```csharp
var result = await repo.GetObjectsAsync<CustomerEntity>(
    "PartitionKey eq 'tenant-1' and Status eq 'Active'", ct);
```

### Desde dentro de Akay.To.Azure (ITableStorageExtendedRepository)

#### 10. Obtener entidades ITableEntity

```csharp
var extended = factory.Create("orders") as ITableStorageExtendedRepository;
var result = await extended!.GetEntitiesAsync<OrderEntity>(
    $"PartitionKey eq 'cust-123'", ct);

if (result.TryGetValue(out var orders))
    Console.WriteLine($"Encontradas {orders.Count()} ordenes");
```

#### 11. Upsert de ITableEntity directa

```csharp
var order = new OrderEntity
{
    PartitionKey = "orders-2026",
    RowKey = Guid.NewGuid().ToString(),
    Amount = 149.99m,
    Status = "Pending"
};

var result = await extended.UpsertEntityAsync(order, ct);
```

#### 12. Paginacion con continuationToken

```csharp
var filter = TableStorageFilter.PartitionKey("orders-2026");
string? token = null;

do
{
    var pageResult = await extended.GetPaginatedEntitiesAsync<OrderEntity>(
        filter, continuationToken: token, pageSize: 50, ct);

    if (pageResult.TryGetValue(out var page))
    {
        foreach (var order in page.Data)
            ProcessOrder(order);

        token = page.HasMoreItems ? page.NextPage : null;
    }
    else
    {
        break;
    }
} while (token is not null);
```

#### 13. GetEntity por clave compuesta

```csharp
var entity = await extended.GetEntityAsync<OrderEntity>(
    "orders-2026", "550e8400-e29b-41d4-a716-446655440000", ct);

if (entity.TryGetValue(out var order) && order is not null)
    Console.WriteLine($"Orden: {order.Amount:C} - {order.Status}");
```

#### 14. GetObject (tipado) desde MemoArray

```csharp
var config = await extended.GetObjectAsync<AppConfig>("app", "config", ct);

if (config.TryGetValue(out var cfg) && cfg is not null)
    Console.WriteLine($"MaxRetries: {cfg.MaxRetries}");
```
