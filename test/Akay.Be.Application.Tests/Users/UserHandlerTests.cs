using Akay.Be.Application.Features.Users;
using Akay.Be.Application.Abstractions.Persistence.Repositories.Identity;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.Be.Domain.Events.Identity;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using Moq;

namespace Akay.Be.Application.Tests.Users;

public sealed class UserHandlerTests
{
    private static readonly Mock<IAdminScopeService> AdminScope = new();
    private static readonly Mock<IUnitOfWork> UnitOfWork = new();
    private static readonly Mock<IUserRepository> UserRepo = new();

    public UserHandlerTests()
    {
        AdminScope.Reset();
        UnitOfWork.Reset();
        UserRepo.Reset();
    }

    [Fact]
    public async Task CreateUser_Should_Create_User_With_Initial_Roles()
    {
        // Arrange
        AdminScope.Setup(x => x.EnsureAdminOfAllCentersAsync(It.Is<IEnumerable<int>>(c => c.Contains(1)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.EmailExistsAsync("new@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        User? capturedUser = null;
        UserRepo.Setup(x => x.Add(It.IsAny<User>())).Callback<User>(u => capturedUser = u);

        var handler = new CreateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object);
        var command = new CreateUserCommand("new@example.com", "John", "Doe", [new CreateUserInitialRole(1, UserRole.Teacher)]);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedUser);
        Assert.Equal("new@example.com", capturedUser!.Email);
        Assert.Null(capturedUser.ExternalId);
        Assert.Single(capturedUser.RoleAssignments);
        Assert.Equal(UserRole.Teacher, capturedUser.RoleAssignments.First().Role);
    }

    [Fact]
    public async Task CreateUser_Should_Fail_When_Local_Email_Exists()
    {
        AdminScope.Setup(x => x.EnsureAdminOfAllCentersAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.EmailExistsAsync("exists@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new CreateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object);
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

        var handler = new CreateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object);
        var command = new CreateUserCommand("sa@example.com", "Super", "Admin", [new CreateUserInitialRole(1, UserRole.SuperAdmin)]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("user.superadmin_not_allowed", result.Error!.Code);
    }

    [Fact]
    public async Task CreateUser_Should_Fail_When_Student_Role()
    {
        AdminScope.Setup(x => x.EnsureAdminOfAllCentersAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new CreateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object);
        var command = new CreateUserCommand("student@example.com", "Student", "User", [new CreateUserInitialRole(1, UserRole.Student)]);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("user.student_not_allowed", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateUser_Should_Update_Profile()
    {
        var user = CreateUserWithExternalId("old@example.com", "Old", "Name");

        AdminScope.Setup(x => x.EnsureCanWriteUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        UserRepo.Setup(x => x.EmailExistsAsync("new@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new UpdateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object);
        var result = await handler.Handle(new UpdateUserCommand(user.Id, "new@example.com", "New", "Name", true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new@example.com", result.Value!.Email);
        Assert.Equal("New", result.Value.FirstName);
        Assert.Null(user.ExternalId);
        var cleanupEvent = Assert.Single(user.AfterSaveDomainEvents.OfType<ExternalIdentityCleanupRequestedOutboxEvent>());
        Assert.Equal("old@example.com", cleanupEvent.Email);
        Assert.Equal(ExternalIdentityCleanupReasons.EmailChanged, cleanupEvent.Reason);
    }

    [Fact]
    public async Task UpdateUser_Should_Not_Queue_When_Email_Does_Not_Change()
    {
        var user = CreateUserWithExternalId("old@example.com", "Old", "Name");

        AdminScope.Setup(x => x.EnsureCanWriteUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        UserRepo.Setup(x => x.EmailExistsAsync("old@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new UpdateUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object);
        var result = await handler.Handle(new UpdateUserCommand(user.Id, "old@example.com", "New", "Name", true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(user.ExternalId);
        Assert.Empty(user.AfterSaveDomainEvents.OfType<ExternalIdentityCleanupRequestedOutboxEvent>());
    }

    [Fact]
    public async Task DeleteUser_Should_Deactivate_And_SoftDelete()
    {
        var user = CreateUserWithExternalId("del@example.com", "Delete", "Me");

        AdminScope.Setup(x => x.EnsureCanWriteUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new DeleteUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object);
        var result = await handler.Handle(new DeleteUserCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);
        Assert.NotNull(user.DeletedAt);
        var cleanupEvent = Assert.Single(user.AfterSaveDomainEvents.OfType<ExternalIdentityCleanupRequestedOutboxEvent>());
        Assert.Equal("del@example.com", cleanupEvent.Email);
        Assert.Equal(ExternalIdentityCleanupReasons.LocalUserDeleted, cleanupEvent.Reason);
    }

    [Fact]
    public async Task DeleteUser_Should_Not_Queue_When_No_ExternalId()
    {
        var user = User.Create("del@example.com", "Delete", "Me");

        AdminScope.Setup(x => x.EnsureCanWriteUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());
        UserRepo.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new DeleteUserCommandHandler(AdminScope.Object, UnitOfWork.Object, UserRepo.Object);
        var result = await handler.Handle(new DeleteUserCommand(user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(user.AfterSaveDomainEvents.OfType<ExternalIdentityCleanupRequestedOutboxEvent>());
    }

    private static User CreateUserWithExternalId(string email, string firstName, string lastName)
    {
        var user = User.Create(email, firstName, lastName);
        user.SetExternalId(Guid.NewGuid());
        return user;
    }
}
