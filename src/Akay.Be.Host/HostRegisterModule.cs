using Akay.Be.Application;
using Akay.Be.Infrastructure;
using Akay.To.Azure.Host;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.Core.Host.DependencyInjection;
using Microsoft.Extensions.Options;
using Akay.To.Core.Application.Contexts;

namespace Akay.Be.Host;

internal static class HostRegisterModule
{
    /// <summary>
    /// Método de configuración
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="appConfigEndpointKey"></param>
    /// <param name="appConfigPrefixKey"></param>
    /// <param name="keyVaultEndpointKey"></param>
    public static void ConfigureServices(this WebApplicationBuilder builder,
                                         string? appConfigEndpointKey = null,
                                         string? appConfigPrefixKey = null,
                                         string? keyVaultEndpointKey = null)
    {
        // Azure App Configuration y Azure Key Vault son opcionales y solo se usaría uno u otro dependiendo de las necesidades,
        // por eso se pueden configurar por variables de entorno o por appsettings.json y si no se configuran no se añaden al pipeline de configuración
        builder.AddAzureAppConfiguration(
            appConfigEndpointKey == null ? null : Environment.GetEnvironmentVariable(appConfigEndpointKey) ?? builder.Configuration[appConfigEndpointKey],
            appConfigPrefixKey == null ? null : Environment.GetEnvironmentVariable(appConfigPrefixKey) ?? builder.Configuration[appConfigPrefixKey]);

        builder.AddAzureKeyVault(keyVaultEndpointKey == null ? null : Environment.GetEnvironmentVariable(keyVaultEndpointKey) ?? builder.Configuration[keyVaultEndpointKey]);

        var settings = builder.AddConfigurations<ApplicationSettings, ApplicationSettingsValidator>();

        builder.LoggerConfiguration(settings?.Application.Name, settings?.CorrelationHeader);

        builder.Services.AddHttpInfrastructure(settings?.CorrelationHeader)
                        .AddHttpApi()
                        .AddExceptionHandlerProblemDetails()
                        .AddCorsOptions(settings?.AllowedHosts)
                        .AddCultureInfo(settings?.CultureInfo)
                        .AddBearerOrApiKeyAuthentication(settings?.Security)
                        .AddOpenApi(settings?.Application, settings?.Security)
                        .AddUserContext()
                        .AddRateLimitPolicies(settings?.RateLimiting, new List<RateLimitPolicySettings>
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
                                        ? userContext.UserId.ToString()
                                        : $"no-writer-{Guid.NewGuid():N}";
                                }
                            }
                        })
                        .AddHealthChecks();

        builder.Services.AddInfrastructureServices(settings)
                        .AddApplicationServices(settings);
    }

    /// <summary>
    /// Método de configuración
    /// </summary>
    /// <param name="app"></param>
    /// <param name="env"></param>
    public static WebApplication Configure(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<IOptions<ApplicationSettings>>();

        app.ConfigureLaunchUrl(app.Environment, settings.Value.Application.Name ?? "API")
           .UseStatusCodePages()
           .UseExceptionHandler()

           .UseRequestLocalization()
           .UseHeaderPropagation()
           .UseHttpsRedirection()
           .UseCors("AllowSpecificOrigins")

           .UseAuthentication()
           .UseAuthorization()
           .UseRateLimiter();

        app.UseHealthChecksEndpoint("/health")
           .MapControllers();

        return app;
    }
}
