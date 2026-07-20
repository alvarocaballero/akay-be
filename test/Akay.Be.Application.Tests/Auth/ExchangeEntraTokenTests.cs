using System.Reflection;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Features.Auth;
using Akay.Be.Domain.Entities.Identity;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Security.Jwt;
using Moq;

namespace Akay.Be.Application.Tests.Auth;

public sealed class ExchangeEntraTokenTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();

    [Fact]
    public async Task Exchange_Should_Return_Token_When_User_Found_By_ExternalId()
    {
        var externalId = Guid.NewGuid();
        var user = CreateUser(7, "user@example.com", "Ada", "Lovelace", externalId);
        _userRepository.Setup(x => x.GetByExternalIdAsync(externalId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _jwtTokenGenerator.Setup(x => x.Generate(It.IsAny<JwtTokenRequest>()))
            .Returns(new JwtTokenResult("token", DateTimeOffset.UtcNow.AddHours(1), 3600));

        var handler = CreateHandler();
        var result = await handler.Handle(new ExchangeEntraTokenCommand(externalId, "user@example.com", "Ada Lovelace", "Ada", "Lovelace"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("token", result.Value!.AccessToken);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Exchange_Should_Update_ExternalId_When_User_Found_By_Email()
    {
        var oldExternalId = Guid.NewGuid();
        var newExternalId = Guid.NewGuid();
        var user = CreateUser(8, "user@example.com", "Ada", "Lovelace", oldExternalId);
        _userRepository.Setup(x => x.GetByExternalIdAsync(newExternalId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _userRepository.Setup(x => x.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _jwtTokenGenerator.Setup(x => x.Generate(It.IsAny<JwtTokenRequest>()))
            .Returns(new JwtTokenResult("token", DateTimeOffset.UtcNow.AddHours(1), 3600));

        var handler = CreateHandler();
        var result = await handler.Handle(new ExchangeEntraTokenCommand(newExternalId, "user@example.com", "Ada Lovelace", "Ada", "Lovelace"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(newExternalId, user.ExternalId);
        _unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Exchange_Should_Return_Forbidden_When_User_Does_Not_Exist()
    {
        var externalId = Guid.NewGuid();
        _userRepository.Setup(x => x.GetByExternalIdAsync(externalId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        _userRepository.Setup(x => x.GetByEmailAsync("missing@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new ExchangeEntraTokenCommand(externalId, "missing@example.com", "Missing User", null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("auth.exchange.user_not_found", result.Error.Code);
    }

    [Fact]
    public async Task Exchange_Should_Return_Forbidden_When_User_Is_Inactive()
    {
        var externalId = Guid.NewGuid();
        var user = CreateUser(9, "inactive@example.com", "Inactive", "User", externalId, isActive: false);
        _userRepository.Setup(x => x.GetByExternalIdAsync(externalId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = CreateHandler();
        var result = await handler.Handle(new ExchangeEntraTokenCommand(externalId, "inactive@example.com", "Inactive User", null, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("auth.exchange.user_inactive", result.Error.Code);
    }

    [Fact]
    public async Task Exchange_Should_Generate_Token_With_Local_UserId()
    {
        var externalId = Guid.NewGuid();
        var user = CreateUser(15, "user@example.com", "Grace", "Hopper", externalId);
        JwtTokenRequest? capturedRequest = null;
        _userRepository.Setup(x => x.GetByExternalIdAsync(externalId, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _jwtTokenGenerator.Setup(x => x.Generate(It.IsAny<JwtTokenRequest>()))
            .Callback<JwtTokenRequest>(request => capturedRequest = request)
            .Returns(new JwtTokenResult("token", DateTimeOffset.UtcNow.AddHours(1), 3600));

        var handler = CreateHandler();
        var result = await handler.Handle(new ExchangeEntraTokenCommand(externalId, "user@example.com", "Grace Hopper", "Grace", "Hopper"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedRequest);
        Assert.Equal(15, capturedRequest!.UserId);
        Assert.Equal("Grace Hopper", capturedRequest.Name);
        Assert.Equal("user@example.com", capturedRequest.Email);
    }

    private ExchangeEntraTokenCommandHandler CreateHandler() =>
        new(_userRepository.Object, _unitOfWork.Object, _jwtTokenGenerator.Object);

    private static User CreateUser(int id,
                                   string email,
                                   string firstName,
                                   string lastName,
                                   Guid? externalId = null,
                                   bool isActive = true)
    {
        var user = User.Create(email, firstName, lastName);

        typeof(User).GetProperty(nameof(User.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(user, id);

        if (externalId.HasValue)
            user.SetExternalId(externalId.Value);

        if (!isActive)
            user.Deactivate();

        return user;
    }
}
