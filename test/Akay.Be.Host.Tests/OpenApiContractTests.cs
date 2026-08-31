using System.Net;
using System.Text.Json;
using Akay.Be.Host.Controllers;
using Akay.To.Azure.Host.Security.EntraId;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.Core.Host.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Host.Tests;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task GeneratedDocumentUsesStableOperationIdsAndExchangeContract()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development"
        });
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddAuthorization();
        builder.Services.AddEntraIdAuthentication(new SecuritySettings
        {
            EntraExternalId = new EntraExternalIdSettings
            {
                Instance = "https://tenant.ciamlogin.com/",
                TenantId = "tenant-id",
                ClientId = "client-id"
            }
        });
        builder.Services.AddOpenApi(new ApplicationInfo { Name = "Test", Version = new Version(1, 0, 0) },
                                    new SecuritySettings { AuthenticationType = AuthenticationType.Bearer });

        var app = builder.Build();
        app.MapControllers();
        app.MapOpenApi("/openapi/{documentName}.json");
        await app.StartAsync(TestContext.Current.CancellationToken);

        var response = await app.GetTestClient().GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var operations = document.RootElement.GetProperty("paths")
            .EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject())
            .Where(operation => operation.Name is "get" or "post" or "put" or "delete" or "patch")
            .Select(operation => operation.Value)
            .ToList();
        var operationIds = operations.Select(operation => operation.GetProperty("operationId").GetString()).ToList();
        var exchange = document.RootElement.GetProperty("paths").GetProperty("/api/auth/exchange").GetProperty("post");
        var exchangeSecurity = exchange.GetProperty("security").EnumerateArray().Single();
        var academicPeriods = document.RootElement.GetProperty("paths")
            .GetProperty("/api/academic-periods")
            .GetProperty("get");
        var responseContent = academicPeriods.GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content");

        Assert.All(operationIds, operationId => Assert.False(string.IsNullOrWhiteSpace(operationId)));
        Assert.Equal(operationIds.Count, operationIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("authExchange", exchange.GetProperty("operationId").GetString());
        Assert.False(exchange.TryGetProperty("requestBody", out _));
        Assert.True(exchangeSecurity.TryGetProperty(EntraIdSchemeNames.EntraId, out _));
        Assert.Single(responseContent.EnumerateObject());
        Assert.True(responseContent.TryGetProperty("application/json", out _));
    }
}
