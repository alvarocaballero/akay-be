# AddRateLimitPolicies

El método de extensión `AddRateLimitPolicies` registra políticas de rate limiting en el contenedor de servicios mediante la API nativa de ASP.NET Core (`AddRateLimiter`). Se encuentra definido en `Akay.To.Core.Host` dentro de la clase `ServiceBuilderExtension`.

Para que las políticas se apliquen, también debe invocarse el middleware `UseRateLimiter()` en el pipeline de la aplicación:

```csharp
app.UseRateLimiter();
```

## Firma del método

```csharp
public static IServiceCollection AddRateLimitPolicies(
    this IServiceCollection services,
    List<RateLimitPolicySettings>? policies,
    List<RateLimitPolicySettings>? additionalPolicies = null)
```

| Parámetro | Descripción |
|---|---|
| `policies` | Políticas cargadas desde configuración (p. ej., `appsettings.json`). Puede ser `null`. |
| `additionalPolicies` | Políticas adicionales añadidas programáticamente. Se concatenan a las de configuración. |

Ambas listas se fusionan: si no hay ninguna política, el método retorna sin registrar nada.

## RateLimitPolicySettings

Cada política se define con las siguientes propiedades (`BaseApplicationSettings.cs:82`):

| Propiedad | Tipo | Valor por defecto | Descripción |
|---|---|---|---|
| `Name` | `string` | `""` | Nombre único de la política. Se usa luego con `[EnableRateLimiting("name")]`. |
| `Type` | `RateLimitType` | `PerPartitionKey` | Cómo se resuelve la clave de partición (ver tabla abajo). |
| `PartitionKey` | `string?` | `null` | Clave fija usada con `PerPartitionKey` o fallback en `PerFunction`. |
| `PartitionKeyResolver` | `Func<HttpContext, string>?` | `null` | Función que calcula la clave dinámicamente (principalmente para `PerFunction`). Solo disponible desde código, no desde JSON. |
| `PermitLimit` | `int` | `100` | Número máximo de peticiones permitidas dentro de la ventana. |
| `IntervalSeconds` | `int` | `60` | Duración de la ventana en segundos. |
| `QueueLimit` | `int` | `0` | Número máximo de peticiones en cola. `0` = sin cola (rechazo inmediato al exceder `PermitLimit`). |

## RateLimitType

Enum definido en `BaseApplicationSettings.cs:100`. Cada valor determina cómo se calcula la clave de partición del rate limiter:

| Valor | Clave de partición (orden de precedencia) |
|---|---|
| `PerUser` | `IUserContext.UserId` del usuario autenticado. |
| `PerEndpoint` | `DisplayName` del endpoint, o `METHOD:path` como fallback. |
| `PerFunction` | `PartitionKeyResolver` → `PartitionKey` → `Name` de la política. |
| `PerPartitionKey` | `PartitionKey` → `Name` de la política. |

## Configuración desde appsettings.json

Las políticas se declaran bajo la sección `RateLimiting` como un array de objetos. Ejemplo (`src/Akay.Be.Host/appsettings.json`):

```json
"RateLimiting": [
  {
    "Name": "per-user",
    "Type": "PerUser",
    "PermitLimit": 100,
    "IntervalSeconds": 60,
    "QueueLimit": 0
  },
  {
    "Name": "per-endpoint",
    "Type": "PerEndpoint",
    "PermitLimit": 200,
    "IntervalSeconds": 20,
    "QueueLimit": 0
  },
  {
    "Name": "health-endpoint",
    "Type": "PerPartitionKey",
    "PartitionKey": "health",
    "PermitLimit": 1,
    "IntervalSeconds": 30,
    "QueueLimit": 0
  }
]
```

Estas políticas se bindean automáticamente mediante `AddConfigurations<ApplicationSettings, ApplicationSettingsValidator>()` a la propiedad `ApplicationSettings.RateLimiting`, y de ahí se pasan a `AddRateLimitPolicies`.

## Políticas adicionales desde código (additionalPolicies)

El segundo parámetro permite inyectar políticas que no pueden expresarse en JSON, por ejemplo porque requieren lógica condicional o acceso a servicios del contenedor de DI.

Ejemplo real en `HostRegisterModule.cs:46` — política para el rol `writer`:

```csharp
builder.Services.AddRateLimitPolicies(
    settings?.RateLimiting,
    new List<RateLimitPolicySettings>
    {
        new()
        {
            Name = "writer-rate-limit",
            Type = RateLimitType.PerFunction,
            PermitLimit = 5,
            IntervalSeconds = 60,
            QueueLimit = 0,
            PartitionKeyResolver = httpContext =>
            {
                var userContext = httpContext.RequestServices
                    .GetRequiredService<IUserContext>();
                var isWriter = userContext.Roles.Any(r =>
                    string.Equals(r, "writer", StringComparison.OrdinalIgnoreCase));
                return isWriter
                    ? userContext.UserId        // Si es writer, se limita por usuario
                    : $"no-writer-{Guid.NewGuid():N}"; // Si no, cada petición tiene clave distinta (sin límite)
            }
        }
    });
```

### Explicación del ejemplo

- **`Type = RateLimitType.PerFunction`**: delega la resolución de la clave en `PartitionKeyResolver`.
- **`PartitionKeyResolver`**: recibe el `HttpContext` y decide la clave de partición:
  - Si el usuario tiene el rol `writer`, devuelve `UserId` → el límite de 5 peticiones por minuto aplica por usuario writer.
  - Si **no** es writer, devuelve un GUID único por petición → cada petición pertenece a una partición distinta, por lo que nunca alcanza el límite (el rate limiting no afecta a no-writers).
- **`PermitLimit = 5` con `IntervalSeconds = 60`**: cada usuario writer puede hacer hasta 5 peticiones por minuto a los endpoints protegidos con esta política.

### Cómo usar la política en un controller

Una vez registrada, se aplica con el atributo `[EnableRateLimiting]`:

```csharp
[EnableRateLimiting("writer-rate-limit")]
[HttpPost]
public async Task<IActionResult> CreateItem([FromBody] CreateItemRequest request)
{
    // ...
}
```

## Ciclo completo

1. **appsettings.json** → define políticas genéricas (`PerUser`, `PerEndpoint`, etc.).
2. **`AddConfigurations`** → bindea la sección `RateLimiting` a `ApplicationSettings.RateLimiting`.
3. **`AddRateLimitPolicies(policies, additionalPolicies)`** → fusiona todas las políticas y llama a `AddRateLimiter` registrando cada una como `FixedWindowLimiter`.
4. **`UseRateLimiter()`** → activa el middleware en el pipeline.
5. **`[EnableRateLimiting("policyName")]`** → asocia un endpoint a una política concreta.
