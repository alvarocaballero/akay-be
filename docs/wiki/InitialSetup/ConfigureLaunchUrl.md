# ConfigureLaunchUrl

El método `ConfigureLaunchUrl` es una extensión de `WebApplication` que configura la URL raíz (`/`) de la aplicación y Swagger UI, con comportamiento distinto según el entorno. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:324`).

## Firma del método

```csharp
public static WebApplication ConfigureLaunchUrl(
    this WebApplication app,
    IWebHostEnvironment env,
    string? applicationName)
```

| Parámetro | Descripción |
|---|---|
| `app` | La instancia de `WebApplication`. |
| `env` | El entorno de hosting (`IWebHostEnvironment`). |
| `applicationName` | Nombre de la aplicación, usado en producción. |

## Comportamiento

### Entorno Development

1. Añade `UseDeveloperExceptionPage()` — página de excepción detallada.
2. Añade `UseSwagger()` — endpoint `/swagger/v1/swagger.json`.
3. Añade `UseSwaggerUI()` — interfaz Swagger en `/swagger`.
4. Mapea `GET /` → redirección a `/swagger` (anónimo).

### Resto de entornos (Staging, Production...)

1. Mapea `GET /` → respuesta JSON anónima con estado del servicio:
   ```json
   {
     "service": "Akay.Be API",
     "status": "running"
   }
   ```

## Configuración en HostRegisterModule

```csharp
app.ConfigureLaunchUrl(app.Environment, settings.Value.Application.Name ?? "API")
```

El nombre de la aplicación se obtiene de `ApplicationSettings.Application.Name`, con fallback `"API"`.

## Ejemplo de respuesta en producción

```
GET /

HTTP/1.1 200 OK
Content-Type: application/json

{
  "service": "Akay.Be API",
  "status": "running"
}
```

## Notas

- Swagger solo está disponible en `Development`. En otros entornos, la documentación interactiva no se expone por seguridad.
- Ambos endpoints raíz permiten acceso anónimo (`.AllowAnonymous()`).
