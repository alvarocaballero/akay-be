using Akay.To.Core.Host;
using FluentValidation;

namespace Akay.Be.Application;

public class ApplicationSettingsValidator : AbstractValidator<ApplicationSettings>
{
    public ApplicationSettingsValidator()
    {
        RuleFor(x => x.AllowedHosts).NotEmpty();

        When(x => x.Security?.AuthenticationType is AuthenticationType.Bearer or AuthenticationType.BearerOrApiKey, () =>
        {
            RuleFor(x => x.Security!.Jwt).NotNull();
            RuleFor(x => x.Security!.Jwt!.Issuer).NotEmpty();
            RuleFor(x => x.Security!.Jwt!.Audience).NotEmpty();
            RuleFor(x => x.Security!.Jwt!.Key).NotEmpty();
        });

        When(x => x.Security?.AuthenticationType is AuthenticationType.ApiKey or AuthenticationType.BearerOrApiKey, () =>
        {
            RuleFor(x => x.Security!.ApiKey).NotNull();
            RuleFor(x => x.Security!.ApiKey!.Header).NotEmpty();
            RuleFor(x => x.Security!.ApiKey!.Key).NotEmpty();
        });
    }
}
