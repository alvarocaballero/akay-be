# AddCorsOptions

El método `AddCorsOptions` configura la política CORS (Cross-Origin Resource Sharing) de la aplicación. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:163`).

## Firma del método

```csharp
public static IServiceCollection AddCorsOptions(
    this IServiceCollection services,
    string? allowedHosts)
```

| Parámetro | Descripción |
|---|---|
| `services` | Colección de servicios de la aplicación. |
| `allowedHosts` | Orígenes permitidos separados por `;` o `,`. Si es `null`, vacío, o contiene `*`, se permite cualquier origen. |

## Comportamiento

1. Divide `allowedHosts` por `;` y `,`, elimina duplicados y espacios.
2. Crea la política CORS con nombre `"AllowSpecificOrigins"`:
   - Si no hay orígenes o se incluye `*`: permite cualquier origen, cabecera y método (`AllowAnyOrigin`, `AllowAnyHeader`, `AllowAnyMethod`).
   - Si hay orígenes específicos: los configura con `WithOrigins`, permitiendo cualquier cabecera y método, y habilitando credenciales.

## Configuración en HostRegisterModule

```csharp
builder.Services.AddHttpInfrastructure(settings?.CorrelationHeader)
                .AddHttpApi()
                .AddExceptionHandlerProblemDetails()
                .AddCorsOptions(settings?.AllowedHosts)
```

## Ejemplo de appsettings.json

```json
{
  "AllowedHosts": "https://example.com;https://app.example.com"
}
```

Con wildcard:

```json
{
  "AllowedHosts": "*"
}
```

## Middleware requerido

```csharp
app.UseCors("AllowSpecificOrigins");
```

El nombre de la política debe coincidir: `"AllowSpecificOrigins"`.
