# AddHttpApi

El método `AddHttpApi` registra los controladores MVC y configura la serialización JSON. Se encuentra en `Akay.To.Core.Host` (`ServiceBuilderExtension.cs:93`).

## Firma del método

```csharp
public static IServiceCollection AddHttpApi(this IServiceCollection services)
```

No recibe parámetros adicionales. Su comportamiento es fijo.

## Comportamiento

1. Llama a `services.AddControllers()`.
2. Configura las opciones de serialización JSON:
   - Añade `JsonStringEnumConverter`: los `enum` se serializan como strings en lugar de números.
   - Establece `PropertyNameCaseInsensitive = true`: la deserialización ignora mayúsculas/minúsculas en nombres de propiedades.

## Configuración en HostRegisterModule

```csharp
builder.Services.AddHttpInfrastructure(settings?.CorrelationHeader)
                .AddHttpApi()
```

Típicamente se encadena tras `AddHttpInfrastructure` para una configuración fluida.

## Ejemplo de serialización

Con `JsonStringEnumConverter`, un enum como:

```csharp
public enum Status { Active, Inactive }
```

Se serializa como `"Active"` en JSON en lugar de `0`.

Con `PropertyNameCaseInsensitive`, el JSON `{ "name": "test" }` se bindea correctamente a una propiedad `Name` en el modelo.
