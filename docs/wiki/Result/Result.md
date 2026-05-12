# Result

## Qué es

`Result` y `Result<TValue>` son **value objects inmutables** que modelan el resultado de una operación: éxito o fallo. Siguen el patrón **Result Pattern** como alternativa a las excepciones para flujo de control, eliminando `null` como señal de error y forzando el manejo explícito de ambos caminos.

`Error` es el **value object** que describe un fallo con código, descripción y tipo semántico.

`ResultHttpMappingExtensions` cierra el ciclo mapeando `Result`/`Result<T>` a respuestas HTTP (`IResult`) con códigos de estado y `ProblemDetails`.

**Paquete:** `Akay.To.Core`
**Namespace base:** `Akay.To.Core.Application.Results`

---

## Por qué usarlo

- **Sin excepciones para flujo:** errores de negocio (validación, not found, conflicto) no lanzan excepciones; se retornan como parte del `Result`.
- **Pattern matching:** `Match` y `Bind` fuerzan a manejar ambos caminos, eliminando errores por omisión.
- **Composición funcional:** `Map` y `Bind` permiten encadenar operaciones sin `if/else` anidados.
- **Mapeo HTTP automático:** `ToOk()`, `ToCreated()`, `ToNoContent()`, etc. convierten `Result` a `IResult` con status codes correctos.

---

## Error

### ErrorType

```csharp
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

### Error record struct

`Error` es un `readonly record struct` inmutable con tres propiedades:

- `Code`: identificador único del error (ej. `"user.not_found"`)
- `Description`: mensaje descriptivo legible
- `Type`: categoría semántica (`ErrorType`)
- `IsNone`: `true` solo para `Error.None` (sin error)

**Validaciones:**
- `Error.None` es el único error con `ErrorType.None` (no se puede crear manualmente).
- Todo error de fallo requiere `Code` y `Description` no vacíos.
- Todo error de fallo requiere un `ErrorType != None`.

### Factory methods estáticos

Cada tipo de error tiene su factory:

```csharp
Error.Validation("user.email.invalid", "El email no tiene formato válido.")
Error.NotFound("user.not_found", "Usuario no encontrado.")
Error.Conflict("user.already_exists", "El usuario ya existe.")
Error.Unauthorized("auth.expired", "Token expirado.")
Error.Forbidden("role.insufficient", "No tienes permisos para esta acción.")
Error.Failure("payment.declined", "El pago fue rechazado.")
Error.Internal("db.connection_failed", "Error inesperado de conexión.")
Error.Timeout("service.timeout", "Timeout al llamar al servicio externo.")
Error.Unavailable("db.unavailable", "Base de datos no disponible.")
Error.Cancelled("operation.cancelled", "La operación fue cancelada por el usuario.")
```

---

## Result (sin valor)

```csharp
public readonly struct Result
```

### Métodos principales

| Método | Descripción |
|---|---|
| `Result.Success()` | Crea un resultado exitoso sin valor. |
| `Result.Failure(error)` | Crea un resultado fallido con el error dado. |
| `result.IsSuccess` | `true` si es éxito. |
| `result.IsFailure` | `true` si es fallo. |
| `result.Error` | El error (lanza si es éxito). |
| `result.TryGetError(out error)` | Intenta obtener el error sin lanzar. |

### Implicit operator

`Result` admite conversión implícita desde `Error`:

```csharp
public static implicit operator Result(Error error) => Failure(error);
```

Esto permite retornar un `Error` directamente en métodos que devuelven `Result`, sin necesidad de llamar a `Result.Failure()`:

```csharp
// Con implicit operator (directo)
Result DeleteUser(Guid id)
{
    if (!userRepo.Exists(id))
        return Error.NotFound("user.not_found", "Usuario no encontrado.");

    userRepo.Delete(id);
    return Result.Success();
}

// Equivalente sin implicit operator
Result DeleteUser(Guid id)
{
    if (!userRepo.Exists(id))
        return Result.Failure(Error.NotFound("user.not_found", "Usuario no encontrado."));

    userRepo.Delete(id);
    return Result.Success();
}
```

### Composición

| Método | Firma | Descripción |
|---|---|---|
| `Map` | `Result<T> Map(Func<T> map)` | Si éxito, ejecuta `map` y devuelve `Result<T>`. Si fallo, propaga el error. |
| `Bind` | `Result Bind(Func<Result> bind)` | Si éxito, ejecuta `bind` (que devuelve otro `Result`). Si fallo, propaga. |
| `Bind<T>` | `Result<T> Bind(Func<Result<T>> bind)` | Idem, con valor. |
| `Tap` | `Result Tap(Action action)` | Ejecuta `action` si éxito (efecto secundario). Retorna el mismo `Result` sin alterar. |
| `Match` | `TOutput Match(onSuccess, onFailure)` | Ejecuta una rama u otra y devuelve un valor. |
| `Match` | `void Match(Action, Action<Error>)` | Ejecuta una rama u otra sin retornar valor (side effects). |
| `MapAsync` | `ValueTask<Result<T>> MapAsync(Func<ValueTask<T>>)` | Versión async de `Map`. |
| `BindAsync` | `ValueTask<Result> BindAsync(Func<ValueTask<Result>>)` | Versión async de `Bind`. |

---

## Result\<TValue\>

```csharp
public readonly struct Result<TValue>
```

### Métodos principales

| Método | Descripción |
|---|---|
| `Result<T>.Success(value)` | Crea resultado exitoso con valor (acepta `null` para tipos referencia). |
| `Result<T>.Failure(error)` | Crea resultado fallido. |
| `result.Value` | El valor (lanza si es fallo). Puede ser `null` si el tipo lo permite. |
| `result.IsSuccess` | `true` si es éxito. |
| `result.IsFailure` | `true` si es fallo. |
| `result.TryGetValue(out value)` | Intenta obtener el valor sin lanzar. |
| `result.TryGetError(out error)` | Intenta obtener el error sin lanzar. |

### Implicit operators

`Result<TValue>` admite dos conversiones implícitas:

```csharp
public static implicit operator Result<TValue>(TValue value) => Success(value);
public static implicit operator Result<TValue>(Error error) => Failure(error);
```

Esto permite retornar un valor directamente o un `Error` directamente en métodos que devuelven `Result<T>`, sin necesidad de llamar a `Success()` o `Failure()`:

```csharp
// Con implicit operators (directo)
Result<User> GetUser(Guid id)
{
    var user = userRepo.Find(id);
    if (user is null)
        return Error.NotFound("user.not_found", $"Usuario {id} no encontrado.");

    return user; // implícitamente Result<User>.Success(user)
}

// Equivalente sin implicit operators
Result<User> GetUser(Guid id)
{
    var user = userRepo.Find(id);
    if (user is null)
        return Result<User>.Failure(Error.NotFound("user.not_found", $"Usuario {id} no encontrado."));

    return Result<User>.Success(user);
}
```

Ambos operadores son particularmente útiles en composición con `Bind` y `Map`:

```csharp
Result<User> GetUser(Guid id) => userRepo.Find(id) is { } user
    ? user                                      // implícito → Result<User>.Success(user)
    : Error.NotFound("user.not_found", "Not found");  // implícito → Result<User>.Failure(...)

Result<Order> ProcessOrder(OrderRequest request)
{
    return ValidateRequest(request)                              // Result<Request>
        .Bind(valid => ReserveInventory(valid))                  // Result<Reservation>
        .Bind(reserved => ChargePayment(reserved))               // Result<Payment>
        .Map(charged => CreateOrder(charged));                   // Result<Order>
    // Cada paso puede retornar valor o Error directamente gracias a los implicit operators.
}
```

### Composición

| Método | Firma | Descripción |
|---|---|---|
| `Map` | `Result<TNext> Map(Func<T?, TNext> map)` | Transforma el valor si éxito. |
| `Bind` | `Result Bind(Func<T?, Result> bind)` | Encadena con operación sin valor. |
| `Bind<TNext>` | `Result<TNext> Bind(Func<T?, Result<TNext>> bind)` | Encadena con operación con valor. |
| `Tap` | `Result<T> Tap(Action<T?> action)` | Ejecuta `action` si éxito (efecto secundario). Retorna el mismo `Result`. |
| `Match` | `TOutput Match(onSuccess, onFailure)` | Pattern match sobre éxito/fallo con retorno. |
| `Match` | `void Match(Action<T?>, Action<Error>)` | Pattern match sin retornar valor (side effects). |
| `MapAsync` | `ValueTask<Result<TNext>> MapAsync(Func<T?, ValueTask<TNext>>)` | Transformación async. |
| `BindAsync` | `ValueTask<Result> BindAsync(Func<T?, ValueTask<Result>>)` | Encadenamiento async. |

---

## Ejemplos de uso

### Crear un Result de éxito

```csharp
// Usando factory method explícito
var result = Result.Success();
// result.IsSuccess == true, result.IsFailure == false

var userResult = Result<User>.Success(new User("Alice", "alice@example.com"));
// userResult.Value == User { Name = "Alice" }

// Usando implicit operator (solo para Result<T>)
User user = new("Alice", "alice@example.com");
Result<User> implicitResult = user;  // implícitamente Result<User>.Success(user)
```

### Crear un Result de fallo

```csharp
// Usando factory method explícito
var error = Error.NotFound("user.not_found", "Usuario con ID 42 no encontrado.");
var result = Result<User>.Failure(error);
// result.IsFailure == true, result.Error == error

// Usando implicit operator
Result<User> implicitResult = Error.NotFound("user.not_found", "Usuario no encontrado.");
// implicitResult.IsFailure == true

// Para Result (sin tipo)
Result implicitNonTyped = Error.Conflict("user.duplicate", "El email ya está registrado.");
// implicitNonTyped.IsFailure == true
```

### Pattern matching con Match

```csharp
// Con retorno de valor
string message = result.Match(
    onSuccess: user => $"Hola, {user.Name}",
    onFailure: error => $"Error: {error.Description}"
);

// Sin retorno de valor (side effects)
result.Match(
    onSuccess: user => logger.LogInformation("User found: {Name}", user.Name),
    onFailure: error => logger.LogWarning("Operation failed: {Code}", error.Code)
);
```

### Efectos secundarios con Tap

`Tap` permite ejecutar un efecto secundario (logging, métricas, etc.) sin alterar el `Result`. Retorna el mismo `Result` intacto, lo que permite encadenarlo fluídamente:

```csharp
Result<User> GetUser(Guid id)
{
    return userRepo.Find(id) is { } user
        ? Result<User>.Success(user)
        : Error.NotFound("user.not_found", $"Usuario {id} no encontrado.");
}

// Uso con Tap para logging
var result = await GetUser(userId)
    .Tap(user => logger.LogInformation("User loaded: {UserId}", user.Id))
    .Tap(user => metrics.Increment("user.found"))
    .Bind(user => LoadOrdersAsync(user.Id));

// Tap en caso de fallo no ejecuta la acción, pero el Result sigue siendo el mismo
```

### Composición con Map y Bind

```csharp
Result<User> GetUser(Guid id)
{
    return userRepo.Find(id) is { } user
        ? Result<User>.Success(user)
        : Result<User>.Failure(Error.NotFound("user.not_found", $"Usuario {id} no encontrado."));
}

Result<string> GetUserEmail(Guid id)
{
    return GetUser(id).Map(user => user.Email);
    // Si GetUser falla, el error se propaga automáticamente.
    // Si GetUser tiene éxito, se transforma el User → string.
}
```

### Encadenamiento con Bind (railway-oriented)

```csharp
Result<Order> ProcessOrder(OrderRequest request)
{
    return ValidateRequest(request)
        .Bind(valid => ReserveInventory(valid))
        .Bind(reserved => ChargePayment(reserved))
        .Map(charged => CreateOrder(charged));
    // Si cualquier paso falla, el error se propaga y los pasos siguientes no se ejecutan.
}
```

### Versión async

```csharp
async ValueTask<Result<Invoice>> GenerateInvoiceAsync(Guid orderId)
{
    var result = await GetOrder(orderId)
        .BindAsync(order => ValidateOrderAsync(order))
        .MapAsync(valid => GenerateInvoiceAsync(valid));

    return result;
}
```

---

## ResultHttpMappingExtensions

Extiende `Result` y `Result<T>` con métodos para mapear a `IResult` de ASP.NET Core Minimal APIs.

**Namespace:** `Akay.To.Core.Host`

### Métodos de extensión

| Método | Result type | HTTP | Uso típico |
|---|---|---|---|
| `ToOk()` | `Result<T>` | `200 OK` | GET con cuerpo |
| `ToNoContent()` | `Result` | `204 No Content` | DELETE, PUT sin cuerpo |
| `ToCreated(uri)` | `Result<T>` | `201 Created` + header `Location` | POST creación |
| `ToCreated(uriFactory)` | `Result<T>` where `T : notnull` | `201 Created` + location dinámica | POST con ID generado |
| `ToAccepted(uri)` | `Result` / `Result<T>` | `202 Accepted` + header `Location` | Operaciones async |
| `ToFile()` | `Result<HttpFileContent>` | `200 OK` con archivo (o `204` si null) | Descarga de archivos |

### Mapeo de ErrorType a HTTP Status

| ErrorType | HTTP Status |
|---|---|
| `Validation` | `400 Bad Request` |
| `Failure` | `400 Bad Request` |
| `Unauthorized` | `401 Unauthorized` |
| `Forbidden` | `403 Forbidden` |
| `NotFound` | `404 Not Found` |
| `Conflict` | `409 Conflict` |
| `Timeout` | `408 Request Timeout` |
| `Internal` | `500 Internal Server Error` |
| `Unavailable` | `503 Service Unavailable` |
| `Cancelled` | `499 Client Closed Request` |

En caso de fallo, se genera un `ProblemDetails` con:
- `Title` = `error.Code`
- `Detail` = `error.Description`
- `Status` = código HTTP correspondiente
- `Extensions["Error.Type"]` = nombre del `ErrorType`

### Ejemplos de uso en Minimal APIs

```csharp
app.MapGet("/users/{id}", async (Guid id, IUserService service) =>
{
    var result = await service.GetUserAsync(id);
    return result.ToOk();
    // 200 con User en body si éxito
    // 404 con ProblemDetails si not found
});

app.MapPost("/users", async (CreateUserRequest request, IUserService service) =>
{
    var result = await service.CreateUserAsync(request);
    return result.ToCreated(user => $"/users/{user.Id}");
    // 201 con body y Location header si éxito
});

app.MapDelete("/users/{id}", async (Guid id, IUserService service) =>
{
    var result = await service.DeleteUserAsync(id);
    return result.ToNoContent();
    // 204 si éxito, 404/409 si fallo
});

app.MapGet("/reports/{id}/download", async (Guid id, IReportService service) =>
{
    var result = await service.GenerateReportAsync(id);
    return result.ToFile();
    // 200 con archivo (PDF, Excel...) si éxito
});
```

### HttpFileContent

Record usado con `ToFile()` para especificar el contenido del archivo. El contenido es nullable: si el `Result` tiene éxito pero el contenido es `null`, se retorna `204 No Content` automáticamente:

```csharp
public sealed record HttpFileContent
{
    public required ReadOnlyMemory<byte> Content { get; init; }
    public required string ContentType { get; init; }
    public string? FileDownloadName { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public EntityTagHeaderValue? EntityTag { get; init; }
    public bool EnableRangeProcessing { get; init; }
}
```

```csharp
// Ejemplo de creación
var fileContent = new HttpFileContent
{
    Content = Encoding.UTF8.GetBytes("contenido del archivo"),
    ContentType = "text/csv",
    FileDownloadName = "export.csv"
};
var result = Result<HttpFileContent>.Success(fileContent);
return result.ToFile(); // 200 con el archivo

// Si el contenido es null, ToFile() retorna 204 No Content
var result = Result<HttpFileContent>.Success(null);
return result.ToFile(); // 204 No Content
```

---

## Testing

### Test de Errores

```csharp
var error = Error.Validation("field.required", "El campo es obligatorio.");
Assert.Equal(ErrorType.Validation, error.Type);
Assert.Equal("field.required", error.Code);
Assert.False(error.IsNone);
```

### Test de Result

```csharp
// Éxito
var result = Result<int>.Success(42);
Assert.True(result.IsSuccess);
Assert.Equal(42, result.Value);

// Fallo
var failure = Result<int>.Failure(Error.NotFound("x", "y"));
Assert.True(failure.IsFailure);
Assert.True(failure.TryGetError(out var err));
Assert.Equal("x", err.Code);

// Short-circuit en Bind
var called = false;
var final = Result.Failure(Error.Internal("e", "d"))
    .Bind(() => { called = true; return Result.Success(); });
Assert.False(called); // Bind no se ejecutó
```

### Test de ResultHttpMappingExtensions

```csharp
var result = Result<string>.Success("hello");
var httpContext = await ExecuteIResultAsync(result.ToOk());
Assert.Equal(200, httpContext.Response.StatusCode);

var failure = Result<int>.Failure(Error.NotFound("e", "d"));
var httpContext = await ExecuteIResultAsync(failure.ToOk());
Assert.Equal(404, httpContext.Response.StatusCode);
```
