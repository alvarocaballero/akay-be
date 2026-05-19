using Akay.Be.Application;
using Microsoft.Extensions.DependencyInjection;
using Akay.To.Core.Infrastructure.DependencyInjection;
using Akay.To.Azure.Infrastructure.DependencyInjection;

namespace Akay.Be.Infrastructure;

public static class InfrastructureRegisterModule
{
    /// <summary>
    /// Add Infrastructure services
    /// </summary>
    /// <param name="services"></param>
    /// <param name="settings"></param>
    /// <returns></returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, ApplicationSettings? settings)
    {

        //Base
        services
            .AddCache(settings)
            .AddBlobStorage(settings);

        services
            .AddServices()
            .AddRepositories();

        return services;
    }


    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services;
    }
    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services;
    }

}
