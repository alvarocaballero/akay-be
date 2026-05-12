# AddOpenApi

El método `AddOpenApi` configura Swagger / OpenAPI para la aplicación, incluyendo definiciones de seguridad y el filtro de cabecera `Accept-Language`. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:112`).

## Firma del método

```csharp
public static IServiceCollection AddOpenApi(
    this IServiceCollection services,
    Application.Application? applicationSettings,
    SecuritySettings? securitySettings)
```

| Parámetro | Descripción |
|---|---|
| `services` | Colección de servicios de la aplicación. |
| `applicationSettings` | Configuración de la aplicación (nombre y versión). Si es `null`, no se registra Swagger. |
| `securitySettings` | Configuración de seguridad para los esquemas de autenticación en Swagger UI. |

## Comportamiento

1. Si `applicationSettings` es `null`, retorna sin hacer nada.
2. Registra `EndpointsApiExplorer` para el descubrimiento de endpoints.
3. Configura `SwaggerGen`:
   - Documento `"v1"` con `Title = applicationSettings.Name` y `Version = v{version}`.
   - Añade esquemas de seguridad en Swagger UI según `AuthenticationType`:
     - `Bearer` o `BearerOrApiKey` → esquema HTTP Bearer con JWT.
     - `ApiKey` o `BearerOrApiKey` → esquema API Key con cabecera configurable.
   - Añade `AcceptLanguageHeaderOperationFilter` que incluye `Accept-Language` como parámetro en todas las operaciones.

### Esquemas de seguridad en Swagger UI

| AuthenticationType | Esquemas visibles en Swagger UI |
|---|---|
| `None` | Ninguno. |
| `Bearer` | Solo `Bearer` (JWT). |
| `ApiKey` | Solo `ApiKey`. |
| `BearerOrApiKey` | Ambos: `Bearer` y `ApiKey`. |

## Configuración en HostRegisterModule

```csharp
builder.Services.AddHttpInfrastructure(settings?.CorrelationHeader)
                .AddHttpApi()
                .AddExceptionHandlerProblemDetails()
                .AddCorsOptions(settings?.AllowedHosts)
                .AddCultureInfo(settings?.CultureInfo)
                .AddBearerOrApiKeyAuthentication(settings?.Security)
                .AddOpenApi(settings?.Application, settings?.Security)
```

## Ejemplo de appsettings.json

```json
{
  "Application": {
    "Name": "Akay.Be API",
    "Version": "1.0.0"
  }
}
```

## Middleware requerido (solo en Development)

```csharp
if (env.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1"));
}
```

## Accept-Language en Swagger

Cada operación en Swagger UI incluye automáticamente el parámetro `Accept-Language` con valor por defecto `es-ES`:

```
Name: Accept-Language
In: header
Default: es-ES
Required: false
```
