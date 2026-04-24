using FluentValidation;

namespace Akay.Be.Application;

public class ApplicationSettingsValidator : AbstractValidator<ApplicationSettings>
{
    public ApplicationSettingsValidator()
    {
        RuleFor(x => x.AllowedHosts).NotEmpty();
        ////RuleFor(x => x.ProcessId).NotEmpty();
    }
}
