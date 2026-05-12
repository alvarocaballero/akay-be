# AddAzureKeyVault

El método `AddAzureKeyVault` es una extensión de `WebApplicationBuilder` que integra Azure Key Vault en el pipeline de configuración. Se encuentra en `Akay.To.Azure.Host` (`ServiceBuilderExtension.cs:42`).

## Firma del método

```csharp
public static void AddAzureKeyVault(
    this WebApplicationBuilder builder,
    string? keyVaultUrls)
```

| Parámetro | Descripción |
|---|---|
| `builder` | El `WebApplicationBuilder` de la aplicación. |
| `keyVaultUrls` | URLs de los Azure Key Vaults separadas por `;`. Si es `null` o vacío, no se registra. |

## Comportamiento

1. Si `keyVaultUrls` es `null` o vacío, el método retorna sin hacer nada.
2. Crea una instancia de `DefaultAzureCredential` excluyendo credenciales de Visual Studio y VS Code.
3. Divide `keyVaultUrls` por `;` y añade cada Key Vault como fuente de configuración.

## Configuración en HostRegisterModule

```csharp
builder.AddAzureKeyVault(
    keyVaultEndpointKey == null ? null : Environment.GetEnvironmentVariable(keyVaultEndpointKey)
        ?? builder.Configuration[keyVaultEndpointKey]);
```

La URL puede proporcionarse mediante variable de entorno o `appsettings.json`, referenciada por la clave `keyVaultEndpointKey`. Si no se configura, el Key Vault se omite.

## Ejemplo de appsettings.json

```json
{
  "AzureKeyVaultEndpoint": "https://mykeyvault.vault.azure.net"
}
```

Con varios vaults:

```json
{
  "AzureKeyVaultEndpoint": "https://vault1.vault.azure.net;https://vault2.vault.azure.net"
}
```

Y en la llamada:

```csharp
builder.AddAzureKeyVault("AzureKeyVaultEndpoint");
```

## Notas

- Requiere el paquete NuGet `Azure.Identity` y `Azure.Extensions.AspNetCore.Configuration.Secrets`.
- Los valores del Key Vault sobrescriben los de `appsettings.json` si hay conflictos de clave.
