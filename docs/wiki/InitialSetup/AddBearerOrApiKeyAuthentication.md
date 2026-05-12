# AddBearerOrApiKeyAuthentication

El método `AddBearerOrApiKeyAuthentication` configura la autenticación de la aplicación, soportando JWT Bearer, API Key o ambos simultáneamente. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:223`).

## Firma del método

```csharp
public static IServiceCollection AddBearerOrApiKeyAuthentication(
    this IServiceCollection services,
    SecuritySettings? security)
```

| Parámetro | Descripción |
|---|---|
| `services` | Colección de servicios de la aplicación. |
| `security` | Configuración de seguridad. Si es `null` o `AuthenticationType.None`, no se registra autenticación. |

## Modos de autenticación (`AuthenticationType`)

| Modo | Descripción |
|---|---|
| `None` | Sin autenticación. El método retorna sin modificar `services`. |
| `Bearer` | Solo JWT Bearer. Configura `JwtBearerDefaults.AuthenticationScheme`. |
| `ApiKey` | Solo API Key mediante cabecera personalizada. |
| `BearerOrApiKey` | Ambos esquemas. Un `PolicyScheme` decide cuál usar por petición. |

## Comportamiento

1. Si `security` es `null` o `AuthenticationType.None`, retorna sin cambios.
2. Establece una política de fallback que requiere usuario autenticado en todos los endpoints.
3. Según el `AuthenticationType`, delega en una de las siguientes configuraciones:

### ConfigureBearer

- Requiere `security.Jwt` no nulo.
- Configura `JwtBearerDefaults.AuthenticationScheme` como esquema por defecto.
- Valida: issuer, audience, lifetime, y signing key según la configuración JWT.

### ConfigureApiKey

- Requiere `security.ApiKey` no nulo.
- Usa `ApiKeyAuthenticationHandler` con el esquema `"ApiKey"`.

### ConfigureBearerOrApiKey

- Requiere ambos `security.Jwt` y `security.ApiKey`.
- Registra ambos esquemas y usa `BearerOrApiKey` como `PolicyScheme`.
- El selector `SelectAuthenticationScheme` decide:
  - Si la cabecera `Authorization` empieza por `"Bearer "` → JWT Bearer.
  - Si está presente la cabecera `X-Api-Key` → API Key.
  - Por defecto → JWT Bearer.

## Configuración en HostRegisterModule

```csharp
builder.Services.AddHttpInfrastructure(settings?.CorrelationHeader)
                .AddHttpApi()
                .AddExceptionHandlerProblemDetails()
                .AddCorsOptions(settings?.AllowedHosts)
                .AddCultureInfo(settings?.CultureInfo)
                .AddBearerOrApiKeyAuthentication(settings?.Security)
```

## Ejemplo de appsettings.json

```json
{
  "Security": {
    "AuthenticationType": "BearerOrApiKey",
    "Jwt": {
      "Issuer": "https://auth.example.com",
      "Audience": "api",
      "Key": "supersecretkeywithsufficientlength"
    },
    "ApiKey": {
      "Header": "X-Api-Key",
      "Key": "master-api-key-here",
      "Roles": ["machine"]
    }
  }
}
```

## Middleware requerido

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

## Validación JWT

La validación de tokens JWT se configura de forma flexible:

| Propiedad JWT | Validación resultante |
|---|---|
| `Issuer` | Si tiene valor, valida el issuer del token. |
| `Audience` | Si tiene valor, valida la audience del token. |
| `Key` | Si tiene valor, valida la firma con `SymmetricSecurityKey`. |

Esto permite omitir validación de issuer/audience/key si no se configuran.
