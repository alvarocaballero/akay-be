# UserContext

## Qué es

`UserContext` es la implementación concreta de `IUserContext`, una abstracción que encapsula el acceso a los datos del usuario autenticado desde el `HttpContext` de ASP.NET Core. Se registra como servicio **scoped** mediante el método de extensión `AddUserContext()`.

Su propósito es desacoplar el código de aplicación de `HttpContext` directamente, proporcionando una interfaz testeable y tipada para acceder a claims del usuario autenticado.

**Paquete:** `Akay.To.Core`
**Interfaz:** `Akay.To.Core.Application.Contexts.IUserContext`
**Implementación:** `Akay.To.Core.Host.Contexts.UserContext`

---

## Cómo funciona

### Registro en DI

```csharp
// ServiceBuilderExtension.cs
builder.Services.AddUserContext();
```

Esto registra `UserContext` como `Scoped`:

```csharp
services.AddScoped<IUserContext, UserContext>();
```

### Resolución de Claims

`UserContext` recibe `IHttpContextAccessor` por inyección y extrae del `ClaimsPrincipal` los siguientes claims estándar:

| Miembro | Claim usado | Fallback |
|---|---|---|
| `UserId` | `ClaimTypes.NameIdentifier` | `0` (parseo a `int`, `0` si el claim no existe o no es numérico) |
| `Name` | `ClaimTypes.Name` | `string.Empty` |
| `Email` | `ClaimTypes.Email` | `"email"` (claim no estándar), `string.Empty` |
| `Roles` | `ClaimTypes.Role` (todos) | Colección vacía |

### Detección del tipo de autenticación

| Miembro | Condición |
|---|---|
| `IsAuthenticated` | `User.Identity?.IsAuthenticated ?? false` |
| `IsApiKey` | `AuthenticationType == ApiKeyAuthenticationHandler.SchemeName` |
| `IsBearer` | `AuthenticationType == JwtBearerDefaults.AuthenticationScheme` |
| `IsMasterApiKey` | `IsApiKey && UserId == 0` |

### Diagrama de flujo

```
HTTP Request → HttpContext.User (ClaimsPrincipal)
    → IHttpContextAccessor → UserContext
        → IUserContext (expuesto a la capa de aplicación)
```

---

## API de IUserContext

```csharp
public interface IUserContext
{
    bool IsAuthenticated { get; }
    int UserId { get; }
    string Name { get; }
    string Email { get; }
    IEnumerable<string> Roles { get; }
    bool IsApiKey { get; }
    bool IsBearer { get; }
    bool IsMasterApiKey { get; }
}
```

---

## Ejemplos de uso

### Inyección en un servicio de aplicación

```csharp
public class OrdersService
{
    private readonly IUserContext _userContext;

    public OrdersService(IUserContext userContext)
    {
        _userContext = userContext;
    }

    public async Task ProcessOrderAsync(OrderRequest request)
    {
        if (!_userContext.IsAuthenticated)
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        var userId = _userContext.UserId;   // int
        var roles = _userContext.Roles;

        // Lógica de negocio usando userId
    }
}
```

### Control de acceso por roles

```csharp
public bool CanAccessAdminPanel(IUserContext userContext)
{
    return userContext.IsAuthenticated &&
           userContext.Roles.Contains("Admin");
}
```

### Diferenciar API Key vs JWT

```csharp
public string GetAuditLogPrefix(IUserContext userContext)
{
    if (userContext.IsMasterApiKey)
        return "[Master]";

    if (userContext.IsApiKey)
        return $"[API Key: {userContext.Name}]";

    return $"[User: {userContext.Email}]";
}
```

### Uso en un controlador (Minimal API / Controllers)

```csharp
app.MapGet("/profile", (IUserContext userContext) =>
{
    if (!userContext.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        userContext.UserId,
        userContext.Name,
        userContext.Email,
        userContext.Roles
    });
});
```

### Rate Limiting por usuario

```csharp
// ServiceBuilderExtension.cs - AddRateLimitPolicies
RateLimitType.PerUser => httpContext.RequestServices
    .GetRequiredService<IUserContext>().UserId,
```

---

## Testing

Para tests unitarios, basta con mockear `IUserContext`:

```csharp
var mockUserContext = new Mock<IUserContext>();
mockUserContext.Setup(u => u.IsAuthenticated).Returns(true);
mockUserContext.Setup(u => u.UserId).Returns(42);
mockUserContext.Setup(u => u.Roles).Returns(new[] { "User" });

var service = new OrdersService(mockUserContext.Object);
await service.ProcessOrderAsync(request);
```
