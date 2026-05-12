# AddExceptionHandlerProblemDetails

El método `AddExceptionHandlerProblemDetails` registra un manejador global de excepciones que convierte cualquier excepción no controlada en una respuesta `ProblemDetails` (RFC 9457). Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:142`).

## Firma del método

```csharp
public static IServiceCollection AddExceptionHandlerProblemDetails(
    this IServiceCollection services)
```

## Comportamiento

1. Registra `ExceptionToProblemDetailsHandler` como `IExceptionHandler`.
2. Configura `ProblemDetails` con personalización:
   - `traceId`: añade `Activity.Current?.TraceId` (o `HttpContext.TraceIdentifier` como fallback) en `ProblemDetails.Extensions["traceId"]`.
   - `Instance`: se establece como `"{METHOD} {path}"` (ej. `"POST /api/items"`).

### ExceptionToProblemDetailsHandler

Clase interna (`Handlers/ExceptionToProblemDetailsHandler.cs`) que:

- Si la respuesta ya ha comenzado a enviarse, retorna `false` (no puede manejarla).
- Crea un `ProblemDetails` con `Detail = exception.Message`.
- En entorno `Development`, añade `stackTrace` en `Extensions`.
- Delega la escritura al servicio `IProblemDetailsService`.

## Configuración en HostRegisterModule

```csharp
builder.Services.AddHttpInfrastructure(settings?.CorrelationHeader)
                .AddHttpApi()
                .AddExceptionHandlerProblemDetails()
```

## Middleware requerido

```csharp
app.UseExceptionHandler();
```

## Ejemplo de respuesta ante error

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Validation failed: Name is required.",
  "instance": "POST /api/items",
  "traceId": "00-abc123def456-0123456789abcdef-01"
}
```

En entorno `Development` incluye además:

```json
{
  "stackTrace": "at Program.Main() in ..."
}
```
