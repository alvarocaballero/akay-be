# AddAzureAppConfiguration

El método `AddAzureAppConfiguration` es una extensión de `WebApplicationBuilder` que integra Azure App Configuration en el pipeline de configuración de ASP.NET Core. Se encuentra en `Akay.To.Azure.Host` (`ServiceBuilderExtension.cs:10`).

## Firma del método

```csharp
public static void AddAzureAppConfiguration(
    this WebApplicationBuilder builder,
    string? applicationConfigUrl,
    string? applicationPrefixes)
```

| Parámetro | Descripción |
|---|---|
| `builder` | El `WebApplicationBuilder` de la aplicación. |
| `applicationConfigUrl` | URL del endpoint de Azure App Configuration. Si es `null` o vacío, no se registra. |
| `applicationPrefixes` | Claves de prefijo separadas por `;` cuyos valores se cargarán desde App Configuration. Si es `null` o vacío, no se registra. |

## Comportamiento

1. Si `applicationConfigUrl` o `applicationPrefixes` son `null`/vacíos, el método retorna sin hacer nada.
2. Crea una instancia de `DefaultAzureCredential` excluyendo las credenciales de Visual Studio y VS Code (usa la suscripción activa de Azure CLI).
3. Divide `applicationPrefixes` por `;` y, para cada prefijo, selecciona las claves que empiecen por `{prefix}*`.
4. Recorta el prefijo `{prefix}__` de las claves cargadas (ej. `RateLimiting__0__Name` → `0__Name`).

## Configuración en HostRegisterModule

```csharp
builder.AddAzureAppConfiguration(
    appConfigEndpointKey == null ? null : Environment.GetEnvironmentVariable(appConfigEndpointKey)
        ?? builder.Configuration[appConfigEndpointKey],
    appConfigPrefixKey == null ? null : Environment.GetEnvironmentVariable(appConfigPrefixKey)
        ?? builder.Configuration[appConfigPrefixKey]);
```

La URL y los prefijos se pueden proporcionar mediante variables de entorno o `appsettings.json`, referenciados por las claves `appConfigEndpointKey` y `appConfigPrefixKey`. Si no se configuran, Azure App Configuration simplemente se omite.

## Ejemplo de appsettings.json

```json
{
  "AzureAppConfigEndpoint": "https://myappconfig.azconfig.io",
  "AzureAppConfigPrefixes": "AkayBe;Common"
}
```

Y en la llamada:

```csharp
builder.AddAzureAppConfiguration("AzureAppConfigEndpoint", "AzureAppConfigPrefixes");
```

## Notas

- Requiere el paquete NuGet `Azure.Identity` y `Microsoft.Azure.AppConfiguration.AspNetCore`.
- `DefaultAzureCredential` tomará la identidad de la suscripción activa de Azure CLI. Para seleccionarla:
  ```
  az account list --output table
  az account set --subscription "Subscription Name"
  ```
