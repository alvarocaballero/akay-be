using Akay.To.Core.Application.ApplicationSettings;

namespace Akay.Be.Application.Tests;

public sealed class ApplicationSettingsValidatorTests
{
    [Fact]
    public void Validate_WithoutEntraConfiguration_ReportsMissingAuthenticationSettings()
    {
        var settings = new ApplicationSettings { SecuritySettings = new SecuritySettings() };

        var result = new ApplicationSettingsValidator().Validate(settings);

        Assert.Contains(result.Errors, error => error.PropertyName == "SecuritySettings.EntraExternalId");
    }

    [Theory]
    [InlineData("", "tenant-id", "client-id", "Instance")]
    [InlineData("http://tenant.ciamlogin.com/", "tenant-id", "client-id", "Instance")]
    [InlineData("https://tenant.ciamlogin.com/", "", "client-id", "TenantId")]
    [InlineData("https://tenant.ciamlogin.com/", "tenant-id", "", "ClientId")]
    public void Validate_WithIncompleteEntraConfiguration_ReportsAffectedSetting(string instance,
                                                                                string tenantId,
                                                                                string clientId,
                                                                                string property)
    {
        var settings = new ApplicationSettings
        {
            SecuritySettings = new SecuritySettings
            {
                EntraExternalId = new EntraExternalIdSettings { Instance = instance, TenantId = tenantId, ClientId = clientId }
            }
        };

        var result = new ApplicationSettingsValidator().Validate(settings);

        Assert.Contains(result.Errors, error => error.PropertyName == $"SecuritySettings.EntraExternalId.{property}");
    }
}
