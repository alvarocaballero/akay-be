# UseHealthChecksEndpoint

El método `UseHealthChecksEndpoint` es una extensión de `WebApplication` que configura el endpoint de health checks con rate limiting y respuesta JSON personalizada. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:352`).

## Firma del método

```csharp
public static WebApplication UseHealthChecksEndpoint(
    this WebApplication app,
    string endpoint = "/health")
```

| Parámetro | Descripción |
|---|---|
| `app` | La instancia de `WebApplication`. |
| `endpoint` | Ruta del endpoint de health checks. Por defecto `"/health"`. |

## Comportamiento

1. Mapea `GET {endpoint}` usando `MapHealthChecks`.
2. Configura un `ResponseWriter` que genera JSON con:
   - `status`: estado general (`Healthy`, `Degraded`, `Unhealthy`).
   - `checks`: array de checks individuales con `name`, `status` y `exception`.
3. Aplica rate limiting con la política `"health-endpoint"`.
4. Permite acceso anónimo (`.AllowAnonymous()`).

## Configuración en HostRegisterModule

```csharp
app.UseHealthChecksEndpoint("/health")
   .MapControllers();
```

## Ejemplo de respuesta

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "Database",
      "status": "Healthy",
      "exception": null
    },
    {
      "name": "Redis",
      "status": "Healthy",
      "exception": null
    }
  ]
}
```

Si algún check falla:

```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "Database",
      "status": "Unhealthy",
      "exception": "Failed to connect to database"
    }
  ]
}
```

## Rate limiting en health checks

El endpoint aplica la política `"health-endpoint"`, que típicamente se define en `appsettings.json`:

```json
{
  "RateLimiting": [
    {
      "Name": "health-endpoint",
      "Type": "PerPartitionKey",
      "PartitionKey": "health",
      "PermitLimit": 1,
      "IntervalSeconds": 30,
      "QueueLimit": 0
    }
  ]
}
```

Esto limita el endpoint a 1 petición cada 30 segundos, compartida entre todos los clientes (partición fija `"health"`).

## Requisitos previos

- Debe haberse llamado a `AddRateLimiterPolicies` con la política `"health-endpoint"`.
- Debe estar registrado `UseRateLimiter()` en el pipeline.
- Los health checks deben haberse registrado con `AddHealthChecks()` y sus comprobaciones correspondientes.
