using Akay.Be.Application;
using Akay.Be.Application.Features.Users;
using Microsoft.Extensions.Options;

namespace Akay.Be.Application.Tests.Users;

public sealed class UpdateUserProfileCommandValidatorTests
{
    private static UpdateUserProfileCommandValidator CreateValidator(string cultureInfo = "es-ES;en-EN") =>
        new(Options.Create(new ApplicationSettings { CultureInfo = cultureInfo }));

    [Theory]
    [InlineData("es-ES")]
    [InlineData("en-EN")]
    public void Validate_WithSupportedLanguage_IsValid(string language)
    {
        var result = CreateValidator().Validate(new UpdateUserProfileCommand(language, false));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithUnsupportedLanguage_ReportsLanguage()
    {
        var result = CreateValidator().Validate(new UpdateUserProfileCommand("fr-FR", false));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Language");
    }

    [Fact]
    public void Validate_WithEmptyLanguage_ReportsLanguage()
    {
        var result = CreateValidator().Validate(new UpdateUserProfileCommand(string.Empty, false));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Language");
    }

    [Fact]
    public void Validate_WithLanguageExceedingMaxLength_ReportsLanguage()
    {
        var result = CreateValidator().Validate(new UpdateUserProfileCommand("xx-XXXX", false));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Language");
    }
}
