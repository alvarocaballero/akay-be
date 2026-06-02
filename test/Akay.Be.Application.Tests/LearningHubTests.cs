using Akay.Be.Application.Features.LearningHubs;
using Akay.Be.Application.Features.LearningHubs.MediatorExamples;
using Akay.Be.Application.Features.LearningHubs.Responses;
using Akay.To.Core.Application.Abstractions.BlobStorage;
using Akay.To.Core.Application.Contexts;
using Akay.To.Core.Application.Results;
using FluentValidation;
using Moq;

namespace Akay.Be.Application.Tests;

public sealed class CreateLearningHubCommandValidatorTests
{
    private readonly CreateLearningHubCommandValidator _validator = new();

    private static CreateLearningHubCommand CreateCommand(string name, string desc, string addr, string cat) =>
        new(name, desc, addr, cat, Stream.Null, "test.txt", "text/plain");

    [Theory]
    [InlineData("", "Desc", "Addr", "Cat")]
    [InlineData("Name", "", "Addr", "Cat")]
    [InlineData("Name", "Desc", "", "Cat")]
    [InlineData("Name", "Desc", "Addr", "")]
    public void Should_Fail_When_Field_Empty(string name, string desc, string addr, string cat)
    {
        var command = CreateCommand(name, desc, addr, cat);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Should_Fail_When_Name_Exceeds_MaxLength()
    {
        var command = CreateCommand(new string('A', 101), "Desc", "Addr", "Cat");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLearningHubCommand.Name));
    }

    [Fact]
    public void Should_Fail_When_Description_Exceeds_MaxLength()
    {
        var command = CreateCommand("Name", new string('A', 501), "Addr", "Cat");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLearningHubCommand.Description));
    }

    [Fact]
    public void Should_Fail_When_Address_Exceeds_MaxLength()
    {
        var command = CreateCommand("Name", "Desc", new string('A', 201), "Cat");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLearningHubCommand.Address));
    }

    [Fact]
    public void Should_Fail_When_Category_Exceeds_MaxLength()
    {
        var command = CreateCommand("Name", "Desc", "Addr", new string('A', 51));

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateLearningHubCommand.Category));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void Should_Fail_When_FailedAttempts_OutOfRange(int failedAttempts)
    {
        var command = CreateCommand("Name", "Desc", "Addr", "Cat") with { FailedAttempts = failedAttempts };

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Should_Pass_With_Valid_Data()
    {
        var command = CreateCommand("Academia Test", "Descripcion valida", "Calle Falsa 123", "Ciencias");

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}

public sealed class UpdateLearningHubCommandValidatorTests
{
    private readonly UpdateLearningHubCommandValidator _validator = new();

    [Fact]
    public void Should_Fail_When_Id_Zero()
    {
        var command = new UpdateLearningHubCommand(0, new UpdateLearningHubRequest("Name", "Desc", "Addr", "Cat"));

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateLearningHubCommand.Id));
    }

    [Fact]
    public void Should_Fail_When_Id_Negative()
    {
        var command = new UpdateLearningHubCommand(-1, new UpdateLearningHubRequest("Name", "Desc", "Addr", "Cat"));

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("", "Desc", "Addr", "Cat")]
    [InlineData("Name", "", "Addr", "Cat")]
    [InlineData("Name", "Desc", "", "Cat")]
    [InlineData("Name", "Desc", "Addr", "")]
    public void Should_Fail_When_Request_Field_Empty(string name, string desc, string addr, string cat)
    {
        var command = new UpdateLearningHubCommand(1, new UpdateLearningHubRequest(name, desc, addr, cat));

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Should_Pass_With_Valid_Data()
    {
        var command = new UpdateLearningHubCommand(1, new UpdateLearningHubRequest("Name", "Desc", "Addr", "Cat"));

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}

public sealed class GetLearningHubsQueryHandlerTests
{
    private readonly GetLearningHubsQueryHandler _handler = new();

    public GetLearningHubsQueryHandlerTests()
    {
        LearningHubStore.Reset();
    }

    [Fact]
    public async Task Should_Return_All_Hubs_When_No_Filters()
    {
        var query = new GetLearningHubsQuery();

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!);
    }

    [Fact]
    public async Task Should_Filter_By_Category()
    {
        var query = new GetLearningHubsQuery(Category: "Ciencias");

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!, h => Assert.Equal("Ciencias", h.Category));
    }

    [Fact]
    public async Task Should_Filter_By_Status()
    {
        var query = new GetLearningHubsQuery(Status: "active");

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!, h => Assert.Equal("active", h.Status));
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_Match()
    {
        var query = new GetLearningHubsQuery(Category: "NonExistent");

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    [Fact]
    public async Task Should_Filter_By_Category_And_Status()
    {
        var query = new GetLearningHubsQuery(Category: "Idiomas", Status: "active");

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value!, h =>
        {
            Assert.Equal("Idiomas", h.Category);
            Assert.Equal("active", h.Status);
        });
    }
}

public sealed class GetLearningHubQueryHandlerTests
{
    private readonly GetLearningHubQueryHandler _handler = new();

    public GetLearningHubQueryHandlerTests()
    {
        LearningHubStore.Reset();
    }

    [Fact]
    public async Task Should_Return_Hub_When_Exists()
    {
        var query = new GetLearningHubQuery(1);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Id);
        Assert.Equal("Academia Newton", result.Value.Name);
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Does_Not_Exist()
    {
        var query = new GetLearningHubQuery(9999);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}

public sealed class CreateLearningHubCommandHandlerTests
{
    private readonly Mock<ICompensationContext> _mockCompensations = new();
    private readonly Mock<IBlobStorageServiceFactory> _mockBlobFactory = new();
    private readonly Mock<IBlobStorageService> _mockBlob = new();
    private readonly CreateLearningHubCommandHandler _handler;

    public CreateLearningHubCommandHandlerTests()
    {
        LearningHubStore.Reset();
        CreateLearningHubCommandHandler.NotificationAttemptTracker.Reset();

        _mockBlobFactory
            .Setup(f => f.CreateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockBlob.Object);

        _mockBlob
            .Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://localhost/devstoreaccount1/container/blob");

        _handler = new CreateLearningHubCommandHandler(_mockCompensations.Object, _mockBlobFactory.Object);
    }

    [Fact]
    public async Task Should_Create_Hub_When_Valid()
    {
        var command = new CreateLearningHubCommand("Nuevo Centro", "Descripcion nueva", "Calle Nueva 1", "Tecnologia",
            Stream.Null, "test.txt", "text/plain");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(0, result.Value!.Id);
        Assert.Equal("Nuevo Centro", result.Value.Name);
        Assert.Equal("active", result.Value.Status);
    }

    [Fact]
    public async Task Should_Return_Conflict_When_Duplicate_Name()
    {
        var command = new CreateLearningHubCommand("Academia Newton", "Dup", "Addr", "Cat",
            Stream.Null, "test.txt", "text/plain");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task Should_Retry_And_Succeed_When_FailedAttempts_Set()
    {
        CreateLearningHubCommandHandler.NotificationAttemptTracker.Reset();

        var command = new CreateLearningHubCommand("Centro Retry", "Desc", "Addr", "Cat",
            Stream.Null, "test.txt", "text/plain", FailedAttempts: 0);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Centro Retry", result.Value!.Name);
    }
}

public sealed class UpdateLearningHubCommandHandlerTests
{
    private readonly UpdateLearningHubCommandHandler _handler = new();

    public UpdateLearningHubCommandHandlerTests()
    {
        LearningHubStore.Reset();
    }

    [Fact]
    public async Task Should_Update_Hub_When_Exists()
    {
        var command = new UpdateLearningHubCommand(1, new UpdateLearningHubRequest("Nombre Actualizado", "Nueva Desc", "Nueva Addr", "Nueva Cat"));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var getHandler = new GetLearningHubQueryHandler();
        var hub = await getHandler.Handle(new GetLearningHubQuery(1), CancellationToken.None);
        Assert.Equal("Nombre Actualizado", hub.Value!.Name);
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Does_Not_Exist()
    {
        var command = new UpdateLearningHubCommand(9999, new UpdateLearningHubRequest("Name", "Desc", "Addr", "Cat"));

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}

public sealed class DeleteLearningHubCommandHandlerTests
{
    private readonly Mock<ICompensationContext> _mockCompensations = new();
    private readonly Mock<IBlobStorageServiceFactory> _mockBlobFactory = new();
    private readonly Mock<IBlobStorageService> _mockBlob = new();
    private readonly DeleteLearningHubCommandHandler _handler = new();

    public DeleteLearningHubCommandHandlerTests()
    {
        LearningHubStore.Reset();
        CreateLearningHubCommandHandler.NotificationAttemptTracker.Reset();

        _mockBlobFactory
            .Setup(f => f.CreateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_mockBlob.Object);

        _mockBlob
            .Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("http://localhost/devstoreaccount1/container/blob");
    }

    [Fact]
    public async Task Should_Delete_Hub_When_Exists()
    {
        var createHandler = new CreateLearningHubCommandHandler(_mockCompensations.Object, _mockBlobFactory.Object);
        var created = await createHandler.Handle(
            new CreateLearningHubCommand("To Delete", "Desc", "Addr", "Cat",
                Stream.Null, "test.txt", "text/plain"), CancellationToken.None);

        var command = new DeleteLearningHubCommand(created.Value!.Id);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var getHandler = new GetLearningHubQueryHandler();
        var hub = await getHandler.Handle(new GetLearningHubQuery(created.Value.Id), CancellationToken.None);
        Assert.True(hub.IsFailure);
    }

    [Fact]
    public async Task Should_Return_NotFound_When_Does_Not_Exist()
    {
        var command = new DeleteLearningHubCommand(9999);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}

public sealed class SearchLearningHubsStreamHandlerTests
{
    private readonly SearchLearningHubsStreamHandler _handler = new();

    public SearchLearningHubsStreamHandlerTests()
    {
        LearningHubStore.Reset();
    }

    [Fact]
    public async Task Should_Stream_All_When_SearchTerm_Empty()
    {
        var request = new SearchLearningHubsStreamRequest("");
        var items = new List<LearningHubStreamItem>();

        await foreach (var item in _handler.Handle(request, CancellationToken.None))
        {
            items.Add(item);
        }

        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("general", i.Relevance));
    }

    [Fact]
    public async Task Should_Return_Matching_Hubs_By_Name()
    {
        var request = new SearchLearningHubsStreamRequest("Newton");
        var items = new List<LearningHubStreamItem>();

        await foreach (var item in _handler.Handle(request, CancellationToken.None))
        {
            items.Add(item);
        }

        Assert.NotEmpty(items);
        Assert.Contains(items, i => i.Name.Contains("Newton", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Should_Return_Empty_When_No_Match()
    {
        var request = new SearchLearningHubsStreamRequest("ZZZZZZZ");
        var items = new List<LearningHubStreamItem>();

        await foreach (var item in _handler.Handle(request, CancellationToken.None))
        {
            items.Add(item);
        }

        Assert.Empty(items);
    }
}

public sealed class LearningHubResponseTests
{
    [Fact]
    public void Should_Create_Response_With_All_Properties()
    {
        var now = DateTime.UtcNow;
        var response = new LearningHubResponse(1, "Name", "Desc", "Addr", "Cat", "active", now, now);

        Assert.Equal(1, response.Id);
        Assert.Equal("Name", response.Name);
        Assert.Equal("Desc", response.Description);
        Assert.Equal("Addr", response.Address);
        Assert.Equal("Cat", response.Category);
        Assert.Equal("active", response.Status);
        Assert.Equal(now, response.CreatedAt);
        Assert.Equal(now, response.UpdatedAt);
    }
}

public sealed class LearningHubSummaryTests
{
    [Fact]
    public void Should_Create_Summary_With_All_Properties()
    {
        var summary = new LearningHubSummary(1, "Name", "Cat", "active");

        Assert.Equal(1, summary.Id);
        Assert.Equal("Name", summary.Name);
        Assert.Equal("Cat", summary.Category);
        Assert.Equal("active", summary.Status);
    }
}
