using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Akay.Be.Application;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.Be.Application.Notifications;
using Akay.Be.Domain.Entities.Notification;
using Akay.Be.Host.Controllers;
using Akay.Be.Infrastructure.Contexts;
using Akay.Be.Infrastructure.Services;
using Akay.Be.Infrastructure.SignalRHubs;
using Akay.To.Azure.Infrastructure.SignalR;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Security.Jwt;
using Akay.To.Core.Host.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Akay.Be.Host.Tests.Notifications;

public sealed partial class NotificationApiTests
{
    [Theory]
    [InlineData("GET", "/notifications")]
    [InlineData("GET", "/notifications/unread-count")]
    [InlineData("PATCH", "/notifications/1/read")]
    [InlineData("PATCH", "/notifications/read-all")]
    [InlineData("DELETE", "/notifications/1")]
    [InlineData("POST", "/hub/notifications/negotiate?negotiateVersion=1")]
    public async Task Endpoints_RequireAuthentication(string method, string path)
    {
        await using var app = await CreateAppAsync(new TestRepository());
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var client = app.GetTestClient();
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Rest_UsesJwtOwner_NotClientSuppliedUser_AndUniformNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var repository = new TestRepository();
        var own = NewNotification(12);
        var foreign = NewNotification(99);
        repository.Items.AddRange([own, foreign]);
        await using var app = await CreateAppAsync(repository);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(app, 12));

        var page = await client.GetFromJsonAsync<JsonElement>("/notifications?userId=99&PageSize=1&IsRead=false", ct);
        Assert.Equal(own.Id, Assert.Single(page.GetProperty("data").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Equal(1, await client.GetFromJsonAsync<int>("/notifications/unread-count?userId=99", ct));
        using var read = await client.PatchAsync($"/notifications/{own.Id}/read", null, ct);
        using var readAgain = await client.PatchAsync($"/notifications/{own.Id}/read", null, ct);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, readAgain.StatusCode);
        Assert.NotNull(own.ReadAt);
        Assert.Null(foreign.ReadAt);

        foreach (var method in new[] { HttpMethod.Patch, HttpMethod.Delete })
        {
            var suffix = method == HttpMethod.Patch ? "/read" : "";
            using var foreignRequest = new HttpRequestMessage(method, $"/notifications/{foreign.Id}{suffix}");
            using var missingRequest = new HttpRequestMessage(method, $"/notifications/{int.MaxValue}{suffix}");
            using var foreignResponse = await client.SendAsync(foreignRequest, ct);
            using var missingResponse = await client.SendAsync(missingRequest, ct);
            Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
            Assert.Equal(foreignResponse.StatusCode, missingResponse.StatusCode);
            var foreignProblem = await foreignResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            var missingProblem = await missingResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(foreignProblem.GetProperty("title").GetString(), missingProblem.GetProperty("title").GetString());
            Assert.Equal(foreignProblem.GetProperty("detail").GetString(), missingProblem.GetProperty("detail").GetString());
        }

        using var allRead = await client.PatchAsync("/notifications/read-all", null, ct);
        Assert.Equal(HttpStatusCode.NoContent, allRead.StatusCode);
        Assert.Null(foreign.ReadAt);
        using var delete = await client.DeleteAsync($"/notifications/{own.Id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(foreign.Id, Assert.Single(repository.Items).Id);
    }

    [Theory]
    [InlineData("Page=0")]
    [InlineData("PageSize=101")]
    [InlineData("IsAscending=true")]
    public async Task Rest_UsesDispatcherValidation(string query)
    {
        await using var app = await CreateAppAsync(new TestRepository());
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(app, 12));
        using var response = await client.GetAsync("/notifications?" + query, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignalR_ReachesBothConnectionsOfJwtOwner_AndNotAnotherUser()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var app = await CreateAppAsync(new TestRepository());
        using var first = await ConnectAsync(app, 12, ct);
        using var second = await ConnectAsync(app, 12, ct);
        using var other = await ConnectAsync(app, 99, ct);
        var publisher = app.Services.GetRequiredService<INotificationPublisher>();
        var payload = JsonSerializer.SerializeToElement(new { topicId = 42 });

        await publisher.PublishAsync("12", new NotificationMessage("completed", payload, 123, "Done", "Finished"), ct);
        await publisher.PublishAsync("99", new NotificationMessage("other-user", payload), ct);

        foreach (var socket in new[] { first, second })
        {
            var message = await ReadFrameAsync(socket, ct);
            Assert.Equal("echo", message.GetProperty("target").GetString());
            var arguments = message.GetProperty("arguments");
            Assert.Equal("_SYSTEM_", arguments[0].GetString());
            Assert.Equal("completed", arguments[1].GetProperty("type").GetString());
            Assert.Equal(42, arguments[1].GetProperty("payload").GetProperty("topicId").GetInt32());
            Assert.Equal(123, arguments[1].GetProperty("notificationId").GetInt32());
        }
        var foreign = await ReadFrameAsync(other, ct);
        Assert.Equal("other-user", foreign.GetProperty("arguments")[1].GetProperty("type").GetString());
        Assert.False(foreign.GetProperty("arguments")[1].TryGetProperty("notificationId", out _));

        // The notification hub exposes no client-to-user publication surface.
        await SendFrameAsync(first, "{\"type\":1,\"invocationId\":\"1\",\"target\":\"SendToUser\",\"arguments\":[\"99\",\"spoofed\"]}", ct);
        var rejected = await ReadFrameAsync(first, ct);
        Assert.Equal(3, rejected.GetProperty("type").GetInt32());
        Assert.True(rejected.TryGetProperty("error", out _));
    }

    private static async Task<WebApplication> CreateAppAsync(TestRepository repository)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(NotificationsController).Assembly);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpApi().AddExceptionHandlerProblemDetails().AddUserContext<AkayUserContext>();
        builder.Services.AddBearerOrApiKeyAuthentication(new SecuritySettings
        {
            AuthenticationType = AuthenticationType.Bearer,
            Jwt = new JwtSettings
            {
                Issuer = "notifications-tests",
                Audience = "notifications-tests",
                SigningKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32))
            }
        });
        builder.Services.AddApplicationServices(null);
        builder.Services.AddSingleton<INotificationRepository>(repository);
        builder.Services.AddSignalR(new AzureSignalRSettings { UseAzureSignalR = false });
        builder.Services.AddTransient<INotificationPublisher, SignalRNotificationPublisher>();
        builder.Services.AddCors(options => options.AddPolicy("AllowSpecificOrigins", policy => policy.AllowAnyOrigin()));
        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<NotificationHub>("/hub/notifications").RequireCors("AllowSpecificOrigins");
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static string Token(WebApplication app, int userId) =>
        app.Services.GetRequiredService<IJwtTokenGenerator>().Generate(new JwtTokenRequest(userId, "Test", "test@example.test")).AccessToken;

    private static async Task<WebSocket> ConnectAsync(WebApplication app, int userId, CancellationToken cancellationToken)
    {
        var client = app.GetTestServer().CreateWebSocketClient();
        var socket = await client.ConnectAsync(new Uri("ws://localhost/hub/notifications?access_token=" + Token(app, userId)), cancellationToken);
        await SendFrameAsync(socket, "{\"protocol\":\"json\",\"version\":1}", cancellationToken);
        var handshake = await ReadFrameAsync(socket, cancellationToken);
        Assert.Equal(JsonValueKind.Object, handshake.ValueKind);
        Assert.False(handshake.TryGetProperty("error", out _));
        return socket;
    }

    private static async Task SendFrameAsync(WebSocket socket, string json, CancellationToken cancellationToken) =>
        await socket.SendAsync(Encoding.UTF8.GetBytes(json + '\u001e').AsMemory(), WebSocketMessageType.Text, true, cancellationToken);

    private static async Task<JsonElement> ReadFrameAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var buffer = new byte[8192];
        using var data = new MemoryStream();
        while (true)
        {
            var received = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
            Assert.NotEqual(WebSocketMessageType.Close, received.MessageType);
            await data.WriteAsync(buffer.AsMemory(0, received.Count), timeout.Token);
            var text = Encoding.UTF8.GetString(data.ToArray());
            var end = text.IndexOf('\u001e');
            if (end >= 0)
                return JsonSerializer.Deserialize<JsonElement>(text[..end]);
        }
    }

    private static int nextNotificationId;

    private static PersistentNotification NewNotification(int userId)
    {
        var notification = PersistentNotification.Create(userId, "completed", "Done", "Finished", null, "{\"topicId\":42}", DateTimeOffset.UtcNow);
        // The fake repository has no database identity; assign a stable id so ownership checks are meaningful.
        typeof(PersistentNotification).GetProperty(nameof(PersistentNotification.Id))!
                            .GetSetMethod(nonPublic: true)!
                            .Invoke(notification, [Interlocked.Increment(ref nextNotificationId)]);
        return notification;
    }

    private sealed class TestRepository : INotificationRepository
    {
        public List<PersistentNotification> Items { get; } = [];
        public Task<PagedResponse<List<PersistentNotification>>> GetPagedAsync(int userId, bool? isRead, int page, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult(PagedResponse<List<PersistentNotification>>.Create(Items.Where(n => n.UserId == userId && (!isRead.HasValue || n.ReadAt.HasValue == isRead))
                .OrderByDescending(n => n.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList(), pageSize, page, false));
        public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Count(n => n.UserId == userId && n.ReadAt is null));
        public Task<bool> MarkReadAsync(int userId, int id, DateTimeOffset readAt, CancellationToken cancellationToken)
        {
            var item = Items.SingleOrDefault(n => n.UserId == userId && n.Id == id);
            item?.MarkRead(readAt);
            return Task.FromResult(item is not null);
        }
        public Task MarkAllReadAsync(int userId, DateTimeOffset readAt, CancellationToken cancellationToken)
        {
            Items.Where(n => n.UserId == userId).ToList().ForEach(n => n.MarkRead(readAt));
            return Task.CompletedTask;
        }
        public Task<bool> DeleteForUserAsync(int userId, int id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.RemoveAll(n => n.UserId == userId && n.Id == id) > 0);
        public Task PersistAsync(PersistentNotification notification, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PersistentNotification?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Add(PersistentNotification entity) => throw new NotSupportedException();
        public void Update(PersistentNotification entity) => throw new NotSupportedException();
        public void Remove(PersistentNotification entity) => throw new NotSupportedException();
        public void RemoveRange(IEnumerable<PersistentNotification> entities) => throw new NotSupportedException();
    }
}
