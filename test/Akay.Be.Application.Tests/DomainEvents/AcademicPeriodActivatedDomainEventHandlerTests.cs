using Akay.Be.Application.DomainEvents;
using Akay.Be.Domain.Events.Academic;
using Akay.To.Core.Application.Abstractions.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Application.Tests.DomainEvents;

public sealed class AcademicPeriodActivatedDomainEventHandlerTests
{
    [Fact]
    public async Task Handle_DoesNothing()
    {
        var services = new ServiceCollection();
        services.AddTransient<IDomainEventHandler<AcademicPeriodActivatedDomainEvent>,
                              AcademicPeriodActivatedDomainEventHandler>();

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IDomainEventHandler<AcademicPeriodActivatedDomainEvent>>();

        var domainEvent = new AcademicPeriodActivatedDomainEvent(Guid.NewGuid(), 1, "2026-2027");

        var exception = await Record.ExceptionAsync(() => handler.Handle(domainEvent, TestContext.Current.CancellationToken).AsTask());

        Assert.Null(exception);
    }
}
