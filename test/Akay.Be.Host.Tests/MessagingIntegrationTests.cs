using System.Collections.Concurrent;
using System.Text.Json;
using Akay.Be.Application.Features.LearningHubs.Messaging;
using Akay.Be.Application.Features.TopicContents;
using Akay.Be.Host.Consumers.Messaging;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Abstractions.Messaging;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.Messaging.Rebus.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Rebus.Transport.InMem;

namespace Akay.Be.Host.Tests;

public sealed class MessagingIntegrationTests
{
    [Fact]
    public async Task ConversionCommandUsesConfiguredQueueAndExactWireJson()
    {
        var configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Akay.Be.Host", "appsettings.json"));
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath, TestContext.Current.CancellationToken));
        var route = config.RootElement.GetProperty("MessagingSettings").GetProperty("Routes").GetProperty(typeof(ConversionRequest).FullName!).GetString()!;
        Assert.Equal("convert-to-markdown", route);
        var settings = new MessagingSettings { Transport = MessagingTransportNames.InMemory, InputQueueName = "backend-test" };
        settings.Routes.Add(typeof(ConversionRequest).FullName!, route);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRebusMessaging(settings, typeof(ConversionRequest).Assembly);
        await using var provider = services.BuildServiceProvider();
        var network = provider.GetRequiredService<InMemNetwork>();
        network.CreateQueue(route);
        var request = new ConversionRequest(Guid.NewGuid(), new ConversionSource("blob", "subjects/1/source.pdf"),
                                            "subjects/1/content.md", "document", true, "topic-content", new ConversionPayload(1, 2, 3));
        await provider.GetRequiredService<IMessageBus>().SendAsync(request, TestContext.Current.CancellationToken);
        var message = Assert.Single(network.GetMessages(route));
        using var json = JsonDocument.Parse(message.Body);
        Assert.Equal(request.requestId, json.RootElement.GetProperty("requestId").GetGuid());
        Assert.Equal("subjects/1/source.pdf", json.RootElement.GetProperty("source").GetProperty("blobName").GetString());
        Assert.Equal(3, json.RootElement.GetProperty("payload").GetProperty("userId").GetInt32());
        Assert.Equal(7, json.RootElement.EnumerateObject().Count());
        Assert.Equal(2, json.RootElement.GetProperty("source").EnumerateObject().Count());
        Assert.Equal(3, json.RootElement.GetProperty("payload").EnumerateObject().Count());
        Assert.Empty(network.GetMessages("backend-test"));
    }

    [Fact]
    public async Task Send_LearningHubCreatedMessage_Should_Deliver_To_Consumer_And_Dispatch_Command()
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
        services.AddRebusMessaging(settings, typeof(LearningHubCreatedConsumer).Assembly);

        await using var provider = services.BuildServiceProvider();

        foreach (var hostedService in provider.GetServices<IHostedService>())
            await hostedService.StartAsync(TestContext.Current.CancellationToken);

        var message = new LearningHubCreatedMessage(42, "Integration Test Hub", "Testing real consumer");
        await provider.GetRequiredService<IMessageBus>().SendAsync(message, TestContext.Current.CancellationToken);

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

        services.AddRebusMessaging(settings, typeof(LearningHubCreatedConsumer).Assembly);

        await using var provider = services.BuildServiceProvider();

        var messageBus = provider.GetRequiredService<IMessageBus>();

        var message = new GenerateLearningHubReportMessage(99);
        var exception = await Record.ExceptionAsync(() => messageBus.SendAsync(message, TestContext.Current.CancellationToken));

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
