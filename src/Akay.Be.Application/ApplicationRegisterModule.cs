using Microsoft.Extensions.DependencyInjection;
namespace Akay.Be.Application;

public static class ApplicationRegisterModule
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddServices();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {

        return services;
    }
}
