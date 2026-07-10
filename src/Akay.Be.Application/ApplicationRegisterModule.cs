using Akay.Be.Application.Abstractions.Services;
using Akay.To.Core.Application.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Application;

public static class ApplicationRegisterModule
{
    /// <summary>
    /// Add application services to the DI container
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, ApplicationSettings? settings)
    {

        services.AddDispatcher(assemblies: typeof(ApplicationRegisterModule).Assembly);


        // Configuración con behaviors opcionales
        ////services.AddDispatcher(options =>
        ////{
        ////    options.UseValidationBehavior = false;
        ////    options.UseCacheBehavior = false;
        ////},
        ////assemblies: assembly);

        services.AddServices();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAdminScopeService, Services.AdminScopeService>();
        return services;
    }
}
