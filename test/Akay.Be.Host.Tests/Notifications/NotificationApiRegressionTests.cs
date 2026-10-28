using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Infrastructure.SignalRHubs;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Host.Tests.Notifications;

public sealed partial class NotificationApiTests
{
    [Fact]
    public async Task RejectedRequests_PreserveSnapshotAndNotFoundDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new TestRepository();
        var foreign = NewNotification(99);
        repository.Items.AddRange([NewNotification(12), foreign]);
        await using var app = await CreateAppAsync(repository);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(app, 12));
        string Hash() => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(repository.Items)));
        var before = Hash();

        foreach (var query in new[] { "Page=0", "Page=-1", "PageSize=0", "PageSize=101",
                                      "Page=2147483647&PageSize=100", "Page=x", "IsRead=x", "SortBy=Title", "IsAscending=true" })
        {
            using var response = await client.GetAsync("/notifications?" + query, ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(before, Hash());
        }

        foreach (var method in new[] { HttpMethod.Patch, HttpMethod.Delete })
        {
            foreach (var id in new[] { foreign.Id.ToString(), int.MaxValue.ToString(), "0", "bad-request" })
            {
                var suffix = method == HttpMethod.Patch ? "/read" : "";
                using var request = new HttpRequestMessage(method, "/notifications/" + id + suffix);
                using var response = await client.SendAsync(request, ct);
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
                if (id != "bad-request")
                {
                    var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                    Assert.Equal("Notification not found.", problem.GetProperty("detail").GetString());
                }
                Assert.Equal(before, Hash());
            }
        }

        var page = await client.GetFromJsonAsync<JsonElement>("/notifications?Page=1&PageSize=1&IsRead=false&SortBy=CreatedAt&IsAscending=false", ct);
        Assert.Single(page.GetProperty("data").EnumerateArray());
        Assert.Equal(before, Hash());
    }

    [Fact]
    public async Task Service_RoutesRuntimePayloadToAllOwnerConnections_WithoutPersistenceForEphemeralMessages()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new TestRepository();
        await using var app = await CreateAppAsync(repository);
        using var first = await ConnectAsync(app, 12, ct);
        using var second = await ConnectAsync(app, 12, ct);
        using var other = await ConnectAsync(app, 99, ct);
        await using var scope = app.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var persistent = new StoredNotice(12, "must-not-publish", "Title", "Message", null);
        await Assert.ThrowsAsync<NotSupportedException>(() => service.SendAsync(persistent, ct));
        INotification ephemeral = new TransientNotice(12, "probe-owner", 42);
        await service.SendAsync(ephemeral, ct);
        await service.SendAsync(new TransientNotice(99, "probe-other", 99), ct);

        foreach (var socket in new[] { first, second })
        {
            var frame = await ReadFrameAsync(socket, ct);
            Assert.Equal("echo", frame.GetProperty("target").GetString());
            var dto = frame.GetProperty("arguments")[1];
            Assert.Equal("probe-owner", dto.GetProperty("type").GetString());
            Assert.Equal(42, dto.GetProperty("payload").GetProperty("extra").GetInt32());
            Assert.False(dto.TryGetProperty("notificationId", out _));
        }

        var foreignFrame = await ReadFrameAsync(other, ct);
        Assert.Equal("probe-other", foreignFrame.GetProperty("arguments")[1].GetProperty("type").GetString());
        Assert.Empty(repository.Items);
        Assert.Empty(typeof(NotificationHub).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    private sealed record TransientNotice(int UserId, string Type, int Extra) : INotification;
    private sealed record StoredNotice(int UserId, string Type, string Title, string Message, string? Url) : IPersistentNotification;
}
