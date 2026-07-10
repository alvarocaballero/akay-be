using System.Reflection;
using Akay.Be.Application;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Organization;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Abstractions.Identity;
using Akay.Be.Infrastructure.Identity;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Persistence.Repositories.Academic;
using Akay.Be.Infrastructure.Persistence.Repositories.Identity;
using Akay.Be.Infrastructure.Persistence.Repositories.Organization;
using Akay.Be.Infrastructure.Services;
using Akay.Be.Infrastructure.SignalRHubs;
using Akay.To.Azure.Infrastructure.DependencyInjection;
using Akay.To.Core.Infrastructure.DependencyInjection;
using Akay.To.EF.SqlServer.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Builder;
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
        ArgumentNullException.ThrowIfNull(settings);

        //Base
        services
            .AddCache(settings.CacheSettings)
            .AddAzureBlobStorage(settings.AzureStorageSettings)
            .AddAzureTableStorage(settings.AzureStorageSettings)
            .AddSignalR(settings.AzureSignalRSettings)
            .AddAzureCognitiveSpeechServices()
            .AddAzureCognitiveTranslatorServices()
            .AddHttpClients(settings.HttpClientSettings, settings.Application?.Name, settings.Application?.Version)
            .AddRebusMessaging(settings.MessagingSettings, Assembly.GetEntryAssembly()!)
            .AddSqlServerEFContext<ApplicationDbContext>(settings);

        services
            .AddServices()
            .AddRepositories();

        return services;
    }

    public static WebApplication AddHubs(this WebApplication app)
    {
        app.MapHub<DemoSignalRHub>("/hub/demosignalrhub")
           .RequireCors("AllowSpecificOrigins");

        return app;
    }


    private static IServiceCollection AddServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IDemoSignalRHubService, DemoSignalRHubService>()
            .AddScoped<IIdentityProvisioningService, NoOpIdentityProvisioningService>();
    }
    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        return services
            .AddScoped<ICenterRepository, CenterRepository>()
            .AddScoped<IUserRepository, UserRepository>()
            .AddScoped<IAcademicPeriodRepository, AcademicPeriodRepository>()
            .AddScoped<ICourseRepository, CourseRepository>()
            .AddScoped<ISubjectRepository, SubjectRepository>()
            .AddScoped<IStudentRepository, StudentRepository>();
    }

}
