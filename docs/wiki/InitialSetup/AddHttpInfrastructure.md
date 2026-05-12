# AddHttpInfrastructure

El método `AddHttpInfrastructure` registra servicios de infraestructura HTTP: acceso al `HttpContext` y propagación de cabeceras. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:76`).

## Firma del método

```csharp
public static IServiceCollection AddHttpInfrastructure(
    this IServiceCollection services,
    string? correlationHeader)
```

| Parámetro | Descripción |
|---|---|
| `services` | Colección de servicios de la aplicación. |
| `correlationHeader` | Nombre de la cabecera de correlación a propagar. Si es `null`, no se añade. |

## Comportamiento

1. Registra `IHttpContextAccessor` (y su implementación) en el contenedor de DI.
2. Configura `HeaderPropagation` para propagar automáticamente la cabecera de correlación si se especifica.

La propagación de cabeceras permite que las peticiones salientes (hechas con `HttpClient`) incluyan automáticamente la cabecera de correlación de la petición entrante.

## Configuración en HostRegisterModule

```csharp
builder.Services.AddHttpInfrastructure(settings?.CorrelationHeader)
```

## Ejemplo de appsettings.json

```json
{
  "CorrelationHeader": "X-Correlation-Id"
}
```

El valor `X-Correlation-Id` pasará como `correlationHeader` a `AddHttpInfrastructure`.

## Middleware requerido

Para que la propagación funcione, debe añadirse el middleware en `Configure`:

```csharp
app.UseHeaderPropagation();
```
