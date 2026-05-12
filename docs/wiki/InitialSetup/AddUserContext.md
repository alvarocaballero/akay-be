# AddUserContext

El método `AddUserContext` registra el contexto de usuario en el contenedor de DI como servicio scoped. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:252`).

## Firma del método

```csharp
public static IServiceCollection AddUserContext(this IServiceCollection services)
```

## Comportamiento

Registra `UserContext` como implementación de `IUserContext` con ciclo de vida `Scoped`:

```csharp
services.AddScoped<IUserContext, UserContext>();
```

## IUserContext

Interfaz definida en `Akay.To.Core.Application.Abstractions`:

| Miembro | Tipo | Descripción |
|---|---|---|
| `IsAuthenticated` | `bool` | Si el usuario está autenticado. |
| `UserId` | `string` | Claim `NameIdentifier` del usuario. |
| `Name` | `string` | Claim `Name` del usuario. |
| `Email` | `string` | Claim `Email` o `"email"` del usuario. |
| `Roles` | `IEnumerable<string>` | Claims de tipo `Role`. |
| `IsApiKey` | `bool` | Si se autenticó mediante API Key. |
| `IsBearer` | `bool` | Si se autenticó mediante JWT Bearer. |
| `IsMasterApiKey` | `bool` | Si es API Key con `UserId == "0"` (master). |

## UserContext

Implementación que obtiene los datos del `HttpContext.User` mediante `IHttpContextAccessor`.

## Configuración en HostRegisterModule

```csharp
builder.Services.AddHttpInfrastructure(settings?.CorrelationHeader)
                .AddHttpApi()
                .AddExceptionHandlerProblemDetails()
                .AddCorsOptions(settings?.AllowedHosts)
                .AddCultureInfo(settings?.CultureInfo)
                .AddBearerOrApiKeyAuthentication(settings?.Security)
                .AddOpenApi(settings?.Application, settings?.Security)
                .AddUserContext()
```

## Uso en application code

```csharp
public class ItemsService
{
    private readonly IUserContext _userContext;

    public ItemsService(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public async Task ProcessAsync()
    {
        if (_userContext.IsAuthenticated)
        {
            var userId = _userContext.UserId;
            var roles = _userContext.Roles;
            // ...
        }
    }
}
```
