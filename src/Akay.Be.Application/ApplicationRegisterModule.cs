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

        ////services.AddDispatcher(options =>
        ////{
        ////    options.UseValidationBehavior = false;
        ////    options.UseCacheBehavior = false;
        ////},
        ////assemblies: typeof(ApplicationRegisterModule).Assembly);

        services.AddServices();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services;
    }
}
