# AddConfigurations

El método `AddConfigurations` es una extensión de `WebApplicationBuilder` que bindea, valida y registra la configuración de la aplicación en una clase fuertemente tipada. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:41`).

## Firma del método

```csharp
public static TSettings AddConfigurations<TSettings, TValidator>(
    this WebApplicationBuilder builder,
    Assembly? userSecretsAnchorType = null)
    where TSettings : BaseApplicationSettings, new()
    where TValidator : IValidator<TSettings>, new()
```

| Parámetro | Descripción |
|---|---|
| `TSettings` | Clase de configuración que extiende `BaseApplicationSettings`. |
| `TValidator` | Validador FluentValidation para `TSettings`. |
| `userSecretsAnchorType` | Assembly usado para localizar User Secrets. Si es `null`, usa el entry assembly. |

## Comportamiento

1. Llama al método privado `AddConfigurations(Assembly?)` que:
   - Añade el archivo `appsettings.{Environment}.json` si no está ya cargado.
   - En entorno `Development`, carga User Secrets del assembly especificado.
   - Normaliza `ApiKey:KeyClients` si viene como JSON plano (flatten a notación de configuración).
2. Bindea `IConfiguration` a `TSettings` mediante `configuration.Get<TSettings>()`.
3. Rellena `Application.Name` y `Application.Version` desde el entry assembly si no están definidos.
4. Ejecuta `TValidator.ValidateAndThrow(settings)` — lanza si la configuración no es válida.
5. Registra `TSettings` como singleton mediante `IOptions<TSettings>`.

## Configuración en HostRegisterModule

```csharp
var settings = builder.AddConfigurations<ApplicationSettings, ApplicationSettingsValidator>();
```

La variable `settings` devuelta es la instancia validada, que luego se pasa al resto de extensiones.

## Ejemplo de appsettings.json

```json
{
  "Application": {
    "Name": "Akay.Be",
    "Description": "Backend API",
    "Version": "1.0.0"
  },
  "AllowedHosts": "https://example.com",
  "CorrelationHeader": "X-Correlation-Id",
  "CultureInfo": "es-ES;en-US",
  "RateLimiting": [
    {
      "Name": "per-user",
      "Type": "PerUser",
      "PermitLimit": 100,
      "IntervalSeconds": 60,
      "QueueLimit": 0
    }
  ],
  "Security": {
    "AuthenticationType": "BearerOrApiKey",
    "Jwt": {
      "Issuer": "https://auth.example.com",
      "Audience": "api",
      "Key": "your-secret-key"
    },
    "ApiKey": {
      "Header": "X-Api-Key",
      "Key": "master-api-key"
    }
  }
}
```

## Validación

El validador (`ApplicationSettingsValidator`) se ejecuta automáticamente. Si alguna propiedad requerida falta o es inválida, se lanza `ValidationException`.
