using System.Collections.Concurrent;
using Akay.Be.Application.Features.Messaging;
using Akay.Be.Host.Consumers.Messaging;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Messaging;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.Core.Application.Results;
using Akay.To.Core.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rebus.Bus;

namespace Akay.Be.Host.Tests;

public sealed class MessagingIntegrationTests
{
    [Fact]
    public async Task Publish_LearningHubCreatedEvent_Should_Deliver_To_UserRegisteredConsumer_And_Dispatch_Command()
    {
        var dispatcher = new RecordingDispatcher();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDispatcher>(dispatcher);

        var settings = new MessagingSettings
        {
            Transport = MessagingTransportNames.InMemory,
            InputQueueName = "test-akaybe-event"
        };

        // Scan the Host assembly where the real consumers live
        services.AddRebusMessaging(settings, typeof(UserRegisteredConsumer).Assembly);

        await using var provider = services.BuildServiceProvider();

        var bus = provider.GetRequiredService<IBus>();
        await bus.Subscribe<LearningHubCreatedEvent>();

        var message = new LearningHubCreatedEvent(42, "Integration Test Hub", "Testing real consumer");
        await bus.Publish(message);

        var command = await dispatcher.WaitForCommand<SendNewLearningHubNotification>(TimeSpan.FromSeconds(3));

        Assert.Equal(42, command.Id);
        Assert.Equal("Integration Test Hub", command.Name);
        Assert.Equal("Testing real consumer", command.Description);
    }

    [Fact]
    public async Task Send_GenerateLearningHubReportMessage_Should_Not_Throw()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var settings = new MessagingSettings
        {
            Transport = MessagingTransportNames.InMemory,
            InputQueueName = "test-akaybe-cmd"
        };

        services.AddRebusMessaging(settings, typeof(UserRegisteredConsumer).Assembly);

        await using var provider = services.BuildServiceProvider();

        var messageBus = provider.GetRequiredService<IMessageBus>();

        var message = new GenerateLearningHubReportMessage(99);
        var exception = await Record.ExceptionAsync(() => messageBus.SendAsync(message, TestContext.Current.CancellationToken));

        Assert.Null(exception);
    }

    [Fact]
    public async Task MessageBus_Should_Publish_LearningHubCreatedEvent_Without_Error()
    {
        var dispatcher = new RecordingDispatcher();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IDispatcher>(dispatcher);

        var settings = new MessagingSettings
        {
            Transport = MessagingTransportNames.InMemory,
            InputQueueName = "test-akaybe-publish"
        };

        services.AddRebusMessaging(settings, typeof(UserRegisteredConsumer).Assembly);

        await using var provider = services.BuildServiceProvider();

        var messageBus = provider.GetRequiredService<IMessageBus>();

        var message = new LearningHubCreatedEvent(1, "Sample", "Desc");
        var exception = await Record.ExceptionAsync(() => messageBus.PublishAsync(message, TestContext.Current.CancellationToken));

        Assert.Null(exception);
    }
}

internal sealed class RecordingDispatcher : IDispatcher
{
    private readonly ConcurrentQueue<object> _requests = new();

    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        _requests.Enqueue(request);
        return ValueTask.FromResult<TResponse>(default!);
    }

    public async Task<T> WaitForCommand<T>(TimeSpan timeout) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_requests.TryDequeue(out var request) && request is T typed)
                return typed;

            await Task.Delay(50);
        }

        throw new TimeoutException($"Command of type {typeof(T).Name} not received within {timeout.TotalSeconds} seconds.");
    }
}
