using Akay.Be.Application;
using Akay.Be.Infrastructure;
using Akay.To.Azure.Host;
using Akay.To.Core.Host;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;

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
                        .AddHealthChecks();

        builder.Services.AddInfrastructureServices(settings)
                        .AddApplicationServices();
    }

    /// <summary>
    /// Método de configuración
    /// </summary>
    /// <param name="app"></param>
    /// <param name="env"></param>
    public static WebApplication Configure(this WebApplication app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage()
                ;
        }

        app.UseStatusCodePages()
           .UseExceptionHandler();

        app.UseRequestLocalization();
        app.UseHeaderPropagation();
        app.UseHttpsRedirection();
        app.UseCors("AllowSpecificOrigins");

        app.UseAuthentication()
           .UseAuthorization();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                var response = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        exception = e.Value.Exception?.Message
                    })
                };

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }).AllowAnonymous();

        app.MapControllers();

        return app;
    }
}
