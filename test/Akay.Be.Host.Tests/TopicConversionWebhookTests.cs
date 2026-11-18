using System.Net;
using System.Net.Http.Json;
using Akay.Be.Application.Features.TopicContents;
using Akay.Be.Host.Controllers;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.Core.Application.Results;
using Akay.To.Core.Host.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Host.Tests;

public sealed class TopicConversionWebhookTests
{
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("wrong", HttpStatusCode.Unauthorized)]
    [InlineData("valid-key", HttpStatusCode.NoContent)]
    public async Task WebhookRequiresApiKeyAndDispatchesBody(string? key, HttpStatusCode expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(TopicConversionWebhookController).Assembly);
        builder.Services.AddBearerOrApiKeyAuthentication(new SecuritySettings
        {
            AuthenticationType = AuthenticationType.ApiKey,
            ApiKey = new ApiKeySettings { Header = "X-Api-Key", Key = "valid-key" }
        });
        var dispatcher = new RecordingDispatcher();
        builder.Services.AddSingleton<IDispatcher>(dispatcher);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync(TestContext.Current.CancellationToken);
        using var client = app.GetTestClient();
        if (key is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", key);
        var id = Guid.NewGuid();
        using var response = await client.PostAsJsonAsync("/api/webhooks/topic-content-converted", new
        {
            eventType = "document.conversion.completed",
            requestId = id,
            source = new { type = "blob", blobName = "source" },
            destinationBlobName = "destination",
            contentType = "document",
            payload = new { topicContentId = 15, topicId = 5, userId = 12 }
        }, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.NoContent)
        {
            Assert.Equal(id, dispatcher.Command!.RequestId);
            Assert.Equal(12, dispatcher.Command.Payload!.userId);
        }
        else
        {
            Assert.Null(dispatcher.Command);
        }
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public SubjectTopicConvertedCommand? Command { get; private set; }
        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            Command = Assert.IsType<SubjectTopicConvertedCommand>(request);
            return ValueTask.FromResult((TResponse)(object)Result.Success());
        }
    }
}
