using Akay.Be.Application.Definitions;
using Akay.Be.Application.Features.LearningHubs.HttpExamples;
using Akay.Be.Application.Features.LearningHubs.Responses;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Akay.Be.Application.Tests;

/// <summary>
/// Tests para los handlers que usan HttpClientExtensions (GetJsonAsync, PostJsonAsync, PutJsonAsync).
/// Cada test usa un FakeHttpHandler para simular respuestas HTTP sin hacer llamadas reales a JSONPlaceholder.
/// </summary>
public sealed class HttpClientExtensionHandlerTests
{
    private readonly Mock<IHttpClientFactory> _mockFactory = new();

    public HttpClientExtensionHandlerTests()
    {
        _mockFactory
            .Setup(f => f.CreateClient(HttpClientNames.JsonPlaceholder))
            .Returns(() => new HttpClient(new FakeHttpHandler()));
    }

    private void SetupFactoryWithHandler(FakeHttpHandler handler)
    {
        _mockFactory
            .Setup(f => f.CreateClient(HttpClientNames.JsonPlaceholder))
            .Returns(() => new HttpClient(handler)
            {
                BaseAddress = new Uri("https://JsonPlaceholder.org/")
            });
    }

    // --- GetPostsHandler ---

    [Fact]
    public async Task GetPostsHandler_Should_Return_Posts_When_Success()
    {
        var posts = new List<PostResponse>
        {
            new(1, "Title 1", "Body 1", 1),
            new(2, "Title 2", "Body 2", 2)
        };

        var handler = new FakeHttpHandler(HttpStatusCode.OK, posts);
        SetupFactoryWithHandler(handler);

        var getPostsHandler = new GetPostsHandler(_mockFactory.Object);
        var result = await getPostsHandler.Handle(new GetPostsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("Title 1", result.Value[0].Title);
    }

    [Fact]
    public async Task GetPostsHandler_Should_Return_Failure_When_Server_Error()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError);
        SetupFactoryWithHandler(handler);

        var getPostsHandler = new GetPostsHandler(_mockFactory.Object);
        var result = await getPostsHandler.Handle(new GetPostsQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("http.500", result.Error.Code);
    }

    // --- GetPostByIdHandler ---

    [Fact]
    public async Task GetPostByIdHandler_Should_Return_Post_When_Found()
    {
        var post = new PostResponse(1, "Title", "Body", 1);
        var handler = new FakeHttpHandler(HttpStatusCode.OK, post);
        SetupFactoryWithHandler(handler);

        var getPostByIdHandler = new GetPostByIdHandler(_mockFactory.Object);
        var result = await getPostByIdHandler.Handle(new GetPostByIdQuery(1), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Id);
        Assert.Equal("Title", result.Value.Title);
    }

    [Fact]
    public async Task GetPostByIdHandler_Should_Return_NotFound_When_Missing()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.NotFound);
        SetupFactoryWithHandler(handler);

        var getPostByIdHandler = new GetPostByIdHandler(_mockFactory.Object);
        var result = await getPostByIdHandler.Handle(new GetPostByIdQuery(9999), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("http.404", result.Error.Code);
    }

    // --- CreatePostHandler ---

    [Fact]
    public async Task CreatePostHandler_Should_Return_Created_Post()
    {
        var created = new PostResponse(101, "New Title", "New Body", 1);
        var handler = new FakeHttpHandler(HttpStatusCode.Created, created);
        SetupFactoryWithHandler(handler);

        var createHandler = new CreatePostHandler(_mockFactory.Object);
        var command = new CreatePostCommand("New Title", "New Body", 1);
        var result = await createHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Title", result.Value!.Title);
    }

    [Fact]
    public async Task CreatePostHandler_Should_Return_BadRequest_When_Invalid()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest);
        SetupFactoryWithHandler(handler);

        var createHandler = new CreatePostHandler(_mockFactory.Object);
        var command = new CreatePostCommand("", "", 0);
        var result = await createHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("http.400", result.Error.Code);
    }

    // --- UpdatePostHandler ---

    [Fact]
    public async Task UpdatePostHandler_Should_Return_Updated_Post()
    {
        var updated = new PostResponse(1, "Updated Title", "Updated Body", 1);
        var handler = new FakeHttpHandler(HttpStatusCode.OK, updated);
        SetupFactoryWithHandler(handler);

        var updateHandler = new UpdatePostHandler(_mockFactory.Object);
        var command = new UpdatePostCommand(1, "Updated Title", "Updated Body", 1);
        var result = await updateHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Title", result.Value!.Title);
    }

    [Fact]
    public async Task UpdatePostHandler_Should_Return_NotFound_When_Missing()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.NotFound);
        SetupFactoryWithHandler(handler);

        var updateHandler = new UpdatePostHandler(_mockFactory.Object);
        var command = new UpdatePostCommand(9999, "Title", "Body", 1);
        var result = await updateHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("http.404", result.Error.Code);
    }

    // --- Transport errors ---

    [Fact]
    public async Task GetPostsHandler_Should_Return_Timeout_When_TaskCanceled()
    {
        var handler = new FakeHttpHandler(new TaskCanceledException("timeout"));
        SetupFactoryWithHandler(handler);

        var getPostsHandler = new GetPostsHandler(_mockFactory.Object);
        var result = await getPostsHandler.Handle(new GetPostsQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("http.timeout", result.Error.Code);
    }

    [Fact]
    public async Task GetPostsHandler_Should_Return_Unavailable_When_HttpRequestFails()
    {
        var handler = new FakeHttpHandler(new HttpRequestException("Connection refused"));
        SetupFactoryWithHandler(handler);

        var getPostsHandler = new GetPostsHandler(_mockFactory.Object);
        var result = await getPostsHandler.Handle(new GetPostsQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("http.request_failed", result.Error.Code);
    }

    // --- Response type tests ---

    [Fact]
    public void PostResponse_Should_Have_All_Properties()
    {
        var response = new PostResponse(1, "Title", "Body", 10);

        Assert.Equal(1, response.Id);
        Assert.Equal("Title", response.Title);
        Assert.Equal("Body", response.Body);
        Assert.Equal(10, response.UserId);
    }

    [Fact]
    public void CreatePostCommand_Should_Be_Equal_When_Same_Values()
    {
        var cmd1 = new CreatePostCommand("Title", "Body", 1);
        var cmd2 = new CreatePostCommand("Title", "Body", 1);

        Assert.Equal(cmd1, cmd2);
    }

    [Fact]
    public void UpdatePostCommand_Should_Support_With_Syntax()
    {
        var cmd = new UpdatePostCommand(1, "Old", "Old Body", 1);
        var updated = cmd with { Title = "New" };

        Assert.Equal(1, updated.PostId);
        Assert.Equal("New", updated.Title);
        Assert.Equal("Old Body", updated.Body);
    }
}

/// <summary>
/// Handler HTTP fake para tests. Simula respuestas HTTP sin hacer llamadas reales.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage? _response;
    private readonly Exception? _exception;

    public FakeHttpHandler() => _response = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("{}", Encoding.UTF8, "application/json")
    };

    public FakeHttpHandler(HttpResponseMessage response) => _response = response;

    public FakeHttpHandler(HttpStatusCode statusCode, object? content = null)
    {
        var json = content is not null
            ? JsonSerializer.Serialize(content, new JsonSerializerOptions { PropertyNamingPolicy = null })
            : "{}";

        _response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    public FakeHttpHandler(Exception exception) => _exception = exception;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_exception is not null)
            throw _exception;

        return Task.FromResult(_response!);
    }
}
