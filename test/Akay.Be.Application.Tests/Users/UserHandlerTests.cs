using Akay.Be.Application.Abstractions.Identity;
using Akay.Be.Application.Abstractions.Persistence;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.Users;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using Moq;

namespace Akay.Be.Application.Tests.Users;

public sealed class UserHandlerTests
{
    private static readonly Mock<IAdminScopeService> AdminScope = new();
    private static readonly Mock<IUnitOfWork> UnitOfWork = new();
    private static readonly Mock<IUserRepository> UserRepo = new();
    private static readonly Mock<IIdentityProvisioningService> Identity = new();

    public UserHandlerTests()
    {
        AdminScope.Reset();
        UnitOfWork.Reset();
        UserRepo.Reset();
        Identity.Reset();
    }

    [Fact]
    public async Task CreateUser_Should_Create_User_With_Initial_Roles()
    {
        // Arrange
        AdminScope.Setup(x => x.EnsureAdminOfAllCentersAsync(It.Is<IEnumerable<int>>(c => c.Contains(1)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.EmailExistsAsync("new@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Identity.Setup(x => x.CreateUserAsync("new@example.com", "John", "Doe", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        User? capturedUser = null;
        UserRepo.Setup(x => x.Add(It.IsAny<User>())).Callback<User>(u => capturedUser = u);

        var handler = new CreateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object, Identity.Object);
        var command = new CreateUserCommand("new@example.com", "John", "Doe", [new CreateUserInitialRole(1, UserRole.Teacher)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedUser);
        Assert.Equal("new@example.com", capturedUser!.Email);
        Assert.Single(capturedUser.RoleAssignments);
        Assert.Equal(UserRole.Teacher, capturedUser.RoleAssignments.First().Role);
    }

    [Fact]
    public async Task CreateUser_Should_Fail_When_Email_Exists()
    {
        AdminScope.Setup(x => x.EnsureAdminOfAllCentersAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.EmailExistsAsync("exists@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new CreateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object, Identity.Object);
        var command = new CreateUserCommand("exists@example.com", "John", "Doe", [new CreateUserInitialRole(1, UserRole.Teacher)]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("user.email_exists", result.Error!.Code);
    }

    [Fact]
    public async Task CreateUser_Should_Fail_When_SuperAdmin_Role()
    {
        AdminScope.Setup(x => x.EnsureAdminOfAllCentersAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Identity.Setup(x => x.CreateUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var handler = new CreateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object, Identity.Object);
        var command = new CreateUserCommand("sa@example.com", "Super", "Admin", [new CreateUserInitialRole(1, UserRole.SuperAdmin)]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("user.superadmin_not_allowed", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateUser_Should_Update_Profile()
    {
        var user = CreateUserWithExternalId("old@example.com", "Old", "Name");

        AdminScope.Setup(x => x.EnsureCanAccessUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        UserRepo.Setup(x => x.EmailExistsAsync("new@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        Identity.Setup(x => x.UpdateUserAsync(user.ExternalId!.Value, "new@example.com", "New", "Name", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new UpdateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object, Identity.Object);
        var result = await handler.Handle(new UpdateUserCommand(user.Id, "new@example.com", "New", "Name", true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new@example.com", result.Value!.Email);
        Assert.Equal("New", result.Value.FirstName);
    }

    [Fact]
    public async Task DeleteUser_Should_Deactivate_And_SoftDelete()
    {
        var user = CreateUserWithExternalId("del@example.com", "Delete", "Me");

        AdminScope.Setup(x => x.EnsureCanAccessUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        Identity.Setup(x => x.DeactivateUserAsync(user.ExternalId!.Value, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var handler = new DeleteUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object, Identity.Object);
        var result = await handler.Handle(new DeleteUserCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);
        Assert.NotNull(user.DeletedAt);
    }

    private static User CreateUserWithExternalId(string email, string firstName, string lastName)
    {
        var user = User.Create(email, firstName, lastName);
        user.SetExternalId(Guid.NewGuid());
        return user;
    }
}
