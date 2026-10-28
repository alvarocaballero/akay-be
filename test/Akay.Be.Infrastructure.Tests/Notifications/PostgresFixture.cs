using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Persistence.Repositories.Notification;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.EF.Infrastructure.DbContexts;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Akay.Be.Infrastructure.Tests.Notifications;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string password = Guid.NewGuid().ToString("N");
    private IContainer container = null!;

    public async ValueTask InitializeAsync()
    {
        container = new ContainerBuilder("postgres:17.6")
            .WithImagePullPolicy(PullPolicy.Never)
            .WithCleanUp(false)
            .WithAutoRemove(true)
            .WithEnvironment("POSTGRES_USER", "notifications")
            .WithEnvironment("POSTGRES_PASSWORD", password)
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", "notifications"))
            .Build();
        await container.StartAsync(TestContext.Current.CancellationToken);
    }

    public async Task<ServiceProvider> CreateServicesAsync(bool createSchema = true)
    {
        var ct = TestContext.Current.CancellationToken;
        var database = "notifications_" + Guid.NewGuid().ToString("N");
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = container.Hostname,
            Port = container.GetMappedPublicPort(5432),
            Database = "postgres",
            Username = "notifications",
            Password = password,
            Pooling = false
        };
        await using (var admin = new NpgsqlConnection(connection.ConnectionString))
        {
            await admin.OpenAsync(ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
            await create.ExecuteNonQueryAsync(ct);
        }
        connection.Database = database;
        var services = new ServiceCollection();
        services.AddSingleton<IUserContext, TestUserContext>();
        services.AddSingleton(new DbContextRegistration<ApplicationDbContext>(new DbContextSettings(), (_, _, _) => { }));
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection.ConnectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<INotificationRepository, NotificationRepository>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        if (createSchema)
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync(ct);
        return provider;
    }

    public async ValueTask DisposeAsync() => await container.DisposeAsync();

    private sealed class TestUserContext : IUserContext
    {
        public bool IsAuthenticated => true;
        public int UserId => 12;
        public string Name => "Notifications test";
        public string Email => "notifications@example.test";
        public IEnumerable<string> Roles => [];
    }
}
