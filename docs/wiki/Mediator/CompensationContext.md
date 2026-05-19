# CompensationContext

## Qué es

`ICompensationContext` es un servicio **scoped** que permite a cualquier handler registrar una pila (stack LIFO) de acciones de compensación durante su ejecución. Si el handler falla (excepción o `Result.Failure`), el `CompensationBehavior` ejecuta automáticamente esas acciones para revertir efectos laterales.

Es la implementación del **patrón Saga de compensación** adaptado al pipeline del mediator de `Akay.To.Core`, sin necesidad de librerías externas ni orquestador central.

**Paquete:** `Akay.To.Core`
**Namespace:** `Akay.To.Core.Application.Contexts`
**Interfaz:** `ICompensationContext`
**Implementación:** `CompensationContext` (internal)

---

## Por qué usarlo

- **Rollback automático:** registras lo que hay que deshacer en el momento de crear el recurso, y el behavior lo ejecuta si algo falla después.
- **Código más limpio:** el handler no necesita `try/catch/finally` para limpiar. Las compensaciones se apilan y se olvidan.
- **Orden garantizado:** LIFO — lo último que se creó es lo primero que se deshace.
- **Tolerante a fallos:** si una compensación falla, no impide que se ejecuten las demás. Cada fallo se registra en el log.
- **Trazabilidad:** cada compensación puede tener un nombre descriptivo que aparece en los logs (`LogDebug`/`LogWarning`).

---

## Arquitectura

### Flujo de ejecución

```
Handler crea recursos
  → compensations.Add("Delete X", () => DeleteX())
  → compensations.Add("Rollback Y", () => RollbackY())
  → Handler lanza excepción o retorna Result.Failure
    → CompensationBehavior.RunAsync()
      → Pop "Rollback Y" → ejecutar
      → Pop "Delete X" → ejecutar
      → Stack vacío → Clear()
```

Si el handler completa con éxito → las compensaciones se descartan sin ejecutarse (el behavior llama a `Clear()`).

### Diagrama de colaboración

```
ICompensationContext (injectado en el handler)
        ↓ registra
    Stack<CompensationEntry>
        ↓ consume
CompensationBehavior (en el pipeline)
```

---

## API

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

| Miembro | Descripción |
|---|---|
| `Add(Func<Task>, string?)` | Apila una acción de compensación síncrona/asíncrona con nombre opcional. El nombre aparece en los logs. |
| `Add(Func<CancellationToken, ValueTask>, string?)` | Igual que el anterior pero acepta `CancellationToken`. |
| `HasCompensations` | `true` si el stack tiene al menos una entrada pendiente. |
| `RunAsync(ct)` | Ejecuta todas las compensaciones en orden LIFO. Captura excepciones individuales — una fallida no bloquea a las demás. |
| `Clear()` | Vacía el stack sin ejecutar nada. |

### CompensationContext (implementación interna)

```csharp
internal sealed class CompensationContext(ILogger<CompensationContext> logger) : ICompensationContext
{
    private readonly record struct CompensationEntry(
        string? Name,
        Func<CancellationToken, ValueTask> Action);

    private readonly Stack<CompensationEntry> _compensations = new();
    // ...
}
```

Características de la implementación:

- **Almacenamiento:** `Stack<CompensationEntry>`, donde `CompensationEntry` es un `record struct` con nombre y acción.
- **Logging:** `LogDebug` antes de ejecutar cada compensación, `LogWarning` si falla.
- **Conversión `Func<Task>` → `Func<CancellationToken, ValueTask>`:** el overload que acepta `Func<Task>` lo envuelve en `new ValueTask(compensation())`.
- **Lifetime:** Scoped — mismo ciclo de vida que el `IDispatcher` y los handlers.
- **Thread-safety:** no es thread-safe por diseño; se asume uso secuencial dentro de un mismo scope.

---

## Configuración

### Registro en DI

El `CompensationContext` se registra automáticamente al llamar a `AddDispatcher()` con `UseCompensationBehavior = true` (default):

```csharp
services.AddDispatcher(); // ICompensationContext ya disponible
```

Para desactivarlo:

```csharp
services.AddDispatcher(options =>
{
    options.UseCompensationBehavior = false;
});
```

Esto elimina tanto el registro de `ICompensationContext` como el de `CompensationBehavior`.

---

## Ejemplos de uso

### Caso básico: crear recurso + enviar notificación

```csharp
public sealed record CreateHubCommand(string Name) : ICommand<HubResponse>, ICompensableRequest;

internal sealed class CreateHubHandler(ICompensationContext compensations)
    : ICommandHandler<CreateHubCommand, HubResponse>
{
    public async ValueTask<Result<HubResponse>> Handle(CreateHubCommand request, CancellationToken ct)
    {
        var hub = await CreateHubAsync(request.Name, ct);

        // Si algo falla después de aquí, se borrará el hub
        compensations.Add(
            () => DeleteHubAsync(hub.Id),
            $"Delete hub '{hub.Name}'");

        await SendWelcomeEmailAsync(hub, ct);
        // ↑ Si esto lanza → compensations.RunAsync() borra el hub

        return new HubResponse(hub.Id, hub.Name);
    }
}
```

### Caso avanzado: múltiples pasos con compensaciones encadenadas

```csharp
public async ValueTask<Result> Handle(ProvisionTenantCommand request, CancellationToken ct)
{
    // Paso 1: crear tenant en DB
    var tenant = await CreateTenantAsync(request, ct);
    compensations.Add(() => DeleteTenantAsync(tenant.Id),
        "Delete tenant");

    // Paso 2: crear recursos en Azure
    await ProvisionResourcesAsync(tenant.Id, ct);
    compensations.Add(() => DeprovisionResourcesAsync(tenant.Id),
        "Deprovision Azure resources");

    // Paso 3: enviar notificación
    await NotifyAdminAsync(tenant, ct);
    // Si falla → Deprovision resources → Delete tenant (orden LIFO)

    return Result.Success();
}
```

### Verificar si hay compensaciones pendientes

```csharp
if (compensations.HasCompensations)
{
    _logger.LogWarning("Handler completó con compensations pendientes.");
}
```

### Ejecución manual (casos edge, no recomendado)

```csharp
// Normalmente no se necesita — CompensationBehavior lo hace automáticamente
await compensations.RunAsync(cancellationToken);
compensations.Clear();
```

---

## Testing

### Test unitario de CompensationContext

```csharp
[Fact]
public async Task CompensationContext_Should_Execute_In_LIFO_Order()
{
    var compensations = new CompensationContext(NullLogger<CompensationContext>.Instance);
    var order = new List<string>();

    compensations.Add(() => { order.Add("first"); return Task.CompletedTask; }, "first");
    compensations.Add(() => { order.Add("second"); return Task.CompletedTask; }, "second");

    await compensations.RunAsync();

    Assert.Equal(["second", "first"], order);
    Assert.False(compensations.HasCompensations);
}

[Fact]
public async Task CompensationContext_Should_Survive_Failed_Compensations()
{
    var compensations = new CompensationContext(NullLogger<CompensationContext>.Instance);
    var executed = false;

    compensations.Add(() => throw new Exception("fail"), "failing");
    compensations.Add(() => { executed = true; return Task.CompletedTask; }, "ok");

    await compensations.RunAsync();

    Assert.True(executed);
}
```

### Mock de ICompensationContext

```csharp
var mock = new Mock<ICompensationContext>();
mock.Setup(c => c.HasCompensations).Returns(true);

var handler = new MyHandler(mock.Object);
// ...
mock.Verify(c => c.Add(It.IsAny<Func<Task>>(), It.IsAny<string>()), Times.Once);
```
