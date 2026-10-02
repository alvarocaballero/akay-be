using Akay.To.Core.Application.ApplicationSettings;
using FluentValidation;

namespace Akay.Be.Application;

public class ApplicationSettingsValidator : BaseApplicationSettingsValidator<ApplicationSettings>
{
    public ApplicationSettingsValidator()
    {
        RuleFor(settings => settings.SecuritySettings).NotNull();
        RuleFor(settings => settings.SecuritySettings!.EntraExternalId)
            .NotNull()
            .When(settings => settings.SecuritySettings is not null);

        When(settings => settings.SecuritySettings?.EntraExternalId is not null, () =>
        {
            RuleFor(settings => settings.SecuritySettings!.EntraExternalId!.Instance)
                .Must(instance => Uri.TryCreate(instance, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                .WithMessage("Entra External ID Instance must be an absolute HTTPS URL.");
            RuleFor(settings => settings.SecuritySettings!.EntraExternalId!.TenantId).NotEmpty();
            RuleFor(settings => settings.SecuritySettings!.EntraExternalId!.ClientId).NotEmpty();
        });
    }
}
