using Akay.Be.Application;
using Akay.Be.Host.Controllers;
using Akay.To.Azure.Host.Security.EntraId;
using Akay.To.Azure.Identity.Credentials;
using Akay.To.Core.Host.DependencyInjection;
using Azure.Identity;
using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Host.Tests;

public sealed class StagingAuthenticationTests
{
    [Fact]
    public async Task StagingConfiguration_RegistersEntraAlongsideAkayAuthentication()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Staging.json")
            .Build();
        var settings = configuration.Get<ApplicationSettings>()!;
        var validation = await new ApplicationSettingsValidator().ValidateAsync(settings, TestContext.Current.CancellationToken);
        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Staging" });
        builder.WebHost.UseTestServer();
        var services = builder.Services;
        services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        services.AddBearerOrApiKeyAuthentication(settings.SecuritySettings);
        services.AddEntraIdAuthentication(settings.SecuritySettings);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        var schemes = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.NotNull(await schemes.GetSchemeAsync(EntraIdSchemeNames.EntraId));
        Assert.NotNull(await schemes.GetSchemeAsync("Bearer"));
        Assert.NotNull(await schemes.GetSchemeAsync("ApiKey"));
        Assert.IsType<ManagedIdentityCredential>(AzureCredentialFactory.CreateFromConfiguration(configuration));
        Assert.Equal("d5c8138f-4706-470a-9c67-d43c0841422f", configuration["AZURE_CLIENT_ID"]);
        Assert.Null(configuration["AZURE_CREDENTIAL_MODE"]);

        using var response = await app.GetTestClient().PostAsync(new Uri("/api/auth/exchange", UriKind.Relative),
                                                                 null,
                                                                 TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
