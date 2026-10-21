using Akay.To.Core.Application.ApplicationSettings;

namespace Akay.Be.Application.Tests;

public sealed class ApplicationSettingsValidatorTests
{
    [Fact]
    public void Validate_WithoutEntraConfiguration_IsValid()
    {
        var settings = new ApplicationSettings { SecuritySettings = new SecuritySettings() };

        var result = new ApplicationSettingsValidator().Validate(settings);

        Assert.True(result.IsValid);
        Assert.True(new ApplicationSettingsValidator().Validate(new ApplicationSettings()).IsValid);
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

    [Fact]
    public void Validate_WithInvalidOutboxProcessorSettings_ReportsEveryAffectedSetting()
    {
        ApplicationSettings settings = new()
        {
            SecuritySettings = new SecuritySettings(),
            DbContextSettings = new Dictionary<string, DbContextSettings>
            {
                ["ApplicationDbContext"] = new()
                {
                    ConnectionStringName = "DefaultConnection",
                    Outbox = new DbContextOutboxSettings
                    {
                        Processor = new DbContextOutboxProcessorSettings
                        {
                            PollingInterval = TimeSpan.Zero,
                            BatchSize = 0,
                            RetryInitialDelay = TimeSpan.FromMinutes(10),
                            RetryMaxDelay = TimeSpan.FromMinutes(1),
                        },
                    },
                },
            },
        };

        var result = new ApplicationSettingsValidator().Validate(settings);

        Assert.Contains(result.Errors,
                        error => error.PropertyName.EndsWith("Outbox.Processor.PollingInterval", StringComparison.Ordinal));
        Assert.Contains(result.Errors,
                        error => error.PropertyName.EndsWith("Outbox.Processor.BatchSize", StringComparison.Ordinal));
        Assert.Contains(result.Errors,
                        error => error.PropertyName.EndsWith("Outbox.Processor.RetryMaxDelay", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_WithDefaultOutboxProcessorSettings_ReportsNoOutboxProcessorError()
    {
        ApplicationSettings settings = new()
        {
            SecuritySettings = new SecuritySettings(),
            DbContextSettings = new Dictionary<string, DbContextSettings>
            {
                ["ApplicationDbContext"] = new() { ConnectionStringName = "DefaultConnection" },
            },
        };

        var result = new ApplicationSettingsValidator().Validate(settings);

        Assert.DoesNotContain(result.Errors,
                              error => error.PropertyName.Contains("Outbox.Processor", StringComparison.Ordinal));
    }
}
