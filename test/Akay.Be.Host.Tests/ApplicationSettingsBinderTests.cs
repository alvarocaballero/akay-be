using System.Text;
using Akay.Be.Application;
using Microsoft.Extensions.Configuration;

namespace Akay.Be.Host.Tests;

/// <summary>
/// El binder decide si una seccion existe: un bloque JSON sin claves no crea la instancia, asi que la
/// validacion de la seccion no se ejecuta. Estas pruebas fijan ese comportamiento real.
/// </summary>
public sealed class ApplicationSettingsBinderTests
{
    [Fact]
    public void AnEmptySectionBindsToNullAndIsTreatedAsAbsent()
    {
        ApplicationSettings settings = Bind("{\"SecuritySettings\":{\"EntraExternalId\":{}}}");

        Assert.Null(settings.SecuritySettings!.EntraExternalId);
        Assert.True(new ApplicationSettingsValidator().Validate(settings).IsValid);
    }

    [Fact]
    public void ASectionWithKeysBindsAndIsValidated()
    {
        ApplicationSettings settings = Bind(
            "{\"SecuritySettings\":{\"EntraExternalId\":{\"Instance\":\"\",\"TenantId\":\"\",\"ClientId\":\"\"}}}");

        Assert.NotNull(settings.SecuritySettings!.EntraExternalId);
        Assert.Equal(3, new ApplicationSettingsValidator().Validate(settings).Errors.Count);
    }

    private static ApplicationSettings Bind(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        return new ConfigurationBuilder().AddJsonStream(stream).Build().Get<ApplicationSettings>()!;
    }
}
