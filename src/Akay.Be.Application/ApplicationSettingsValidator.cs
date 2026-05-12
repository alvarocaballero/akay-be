using Akay.To.Core.Application.ApplicationSettings;
using FluentValidation;

namespace Akay.Be.Application;

public class ApplicationSettingsValidator : BaseApplicationSettingsValidator<ApplicationSettings>
{
    public ApplicationSettingsValidator()
    {
        // Reglas específicas de Akay.Be
        ////RuleFor(x => x.AllowedHosts).NotEmpty();
    }
}
