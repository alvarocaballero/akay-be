using Akay.Be.Domain;
using Akay.Be.Domain.Events.Academic;
using Akay.Be.Domain.Events.Identity;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.EF.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Akay.Be.Host.Tests;

/// <summary>
/// Comprueba que el registro del procesador reconoce los eventos Outbox reales de la aplicacion,
/// excluye los eventos de dominio puramente in-process y deja el hosted service listo para el host.
/// </summary>
public sealed class OutboxHostingTests
{
    [Fact]
    public void OutboxEventsAreNotDeliverableWhileTheyOnlyPersist()
    {
        ServiceCollection services = new();

        services.AddOutboxProcessor<ApplicationDbContext>(CreateSettings(), typeof(DomainAssemblyReference).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();
        IReadOnlyDictionary<string, Type> contracts = provider.GetRequiredService<IReadOnlyDictionary<string, Type>>();

        // Los eventos de outbox de Akay.Be hoy solo se persisten. Cuando implementen IOutboxIntegrationEvent
        // entraran en este mapa y esta prueba tendra que actualizarse.
        Assert.Empty(contracts);

        // Un evento de dominio in-process nunca debe poder entregarse por mensajeria.
        Assert.DoesNotContain(typeof(AcademicPeriodActivatedDomainEvent).FullName!, contracts.Keys);
    }

    [Fact]
    public void SignalIsRegisteredAsASingletonSharedByTheHost()
    {
        ServiceCollection services = new();

        services.AddOutboxProcessor<ApplicationDbContext>(CreateSettings(), typeof(DomainAssemblyReference).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<OutboxSignal>(), provider.GetRequiredService<OutboxSignal>());
    }

    [Fact]
    public void ThePackageRegistersTheHostedServiceSoTheHostNeedsNoWorker()
    {
        ServiceCollection services = new();

        // El host registra logging en su composicion (ObservabilityConfiguration); el paquete no lo impone.
        services.AddLogging();
        services.AddOutboxProcessor<ApplicationDbContext>(CreateSettings(), typeof(DomainAssemblyReference).Assembly);

        using ServiceProvider provider = services.BuildServiceProvider();

        IHostedService hostedService = Assert.Single(provider.GetServices<IHostedService>());

        Assert.Equal("OutboxProcessorHostedService`1", hostedService.GetType().Name);
    }

    [Fact]
    public void TheAnchorIsTheAssemblyThatDeclaresTheDeliverableEvents()
    {
        // Si el marcador dejara de vivir junto a los eventos, el procesador no encontraria los contratos.
        Assert.Equal(typeof(DomainAssemblyReference).Assembly, typeof(AcademicPeriodCreatedOutboxEvent).Assembly);
        Assert.Equal(typeof(DomainAssemblyReference).Assembly, typeof(ExternalIdentityCleanupRequestedOutboxEvent).Assembly);
    }

    private static BaseApplicationSettings CreateSettings() => new()
    {
        DbContextSettings = new Dictionary<string, DbContextSettings>
        {
            [nameof(ApplicationDbContext)] = new(),
        },
    };
}
