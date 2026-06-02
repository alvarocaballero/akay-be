using Akay.Be.Application;
using Akay.To.Azure.Infrastructure.DependencyInjection;
using Akay.To.Core.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

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
            .AddCache(settings?.CacheSettings)
            .AddAzureBlobStorage(settings?.AzureStorageSettings)
            .AddTableStorage(settings?.AzureStorageSettings)
            .AddAzureCognitiveSpeechServices()
            .AddAzureCognitiveTranslatorServices()
            .AddHttpClients(settings?.HttpClientSettings, settings?.Application?.Name, settings?.Application?.Version);

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
