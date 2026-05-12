# AddCultureInfo

El método `AddCultureInfo` configura la localización de peticiones (Request Localization) de la aplicación. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:200`).

## Firma del método

```csharp
public static IServiceCollection AddCultureInfo(
    this IServiceCollection services,
    string? cultureInfo)
```

| Parámetro | Descripción |
|---|---|
| `services` | Colección de servicios de la aplicación. |
| `cultureInfo` | Culturas soportadas separadas por `;`. Si es `null`, no se añaden culturas adicionales. |

## Comportamiento

1. Establece la cultura por defecto a `"es-ES"`.
2. Si `cultureInfo` no es `null`, divide por `;` y añade las culturas como soportadas (tanto `SupportedCultures` como `SupportedUICultures`).

## Configuración en HostRegisterModule

```csharp
builder.Services.AddHttpInfrastructure(settings?.CorrelationHeader)
                .AddHttpApi()
                .AddExceptionHandlerProblemDetails()
                .AddCorsOptions(settings?.AllowedHosts)
                .AddCultureInfo(settings?.CultureInfo)
```

## Ejemplo de appsettings.json

```json
{
  "CultureInfo": "es-ES;en-US;fr-FR"
}
```

## Middleware requerido

```csharp
app.UseRequestLocalization();
```

## Uso

El cliente puede especificar la cultura mediante la cabecera `Accept-Language`:

```
GET /api/items HTTP/1.1
Accept-Language: en-US
```

La API de OpenAPI (Swagger) incluye automáticamente esta cabecera como parámetro opcional (ver `AcceptLanguageHeaderOperationFilter`).
