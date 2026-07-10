using Akay.Be.Application.Features.Users;
using Akay.Be.Domain.Entities.Identity;
using Akay.Be.Domain.Enums;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Persistence.Repositories.Identity;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.Core.Application.Requests;
using Akay.To.EF.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Tests.Persistence.Users;

public class UserRepositoryIntegrationTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var userContext = new TestUserContext();
        var registration = new DbContextRegistration<ApplicationDbContext>(
            new DbContextSettings(),
            (_, _, _) => { });

        return new ApplicationDbContext(userContext, registration, options);
    }

    private sealed class TestUserContext : IUserContext
    {
        public bool IsAuthenticated => false;
        public int UserId => 0;
        public string Name => string.Empty;
        public string Email => string.Empty;
        public IEnumerable<string> Roles => Array.Empty<string>();
    }

    [Fact]
    public async Task GetPagedByAdminScopeAsync_FiltersByAdminCenters()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new UserRepository(ctx);

        var u1 = User.Create("u1@example.com", "One", "User");
        u1.AssignRole(1, UserRole.Teacher);

        var u2 = User.Create("u2@example.com", "Two", "User");
        u2.AssignRole(2, UserRole.Student);

        var u3 = User.Create("u3@example.com", "Three", "User");
        u3.AssignRole(1, UserRole.Admin);
        u3.AssignRole(3, UserRole.Student);

        ctx.Users.AddRange(u1, u2, u3);
        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetPagedByAdminScopeAsync(
            new UserListFilter(new HashSet<int> { 1, 3 }, null, null, null, null),
            new PageRequest(1, 10, null, null),
            ct);

        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, u => u.Email == "u1@example.com");
        Assert.Contains(result.Data, u => u.Email == "u3@example.com");
    }

    [Fact]
    public async Task GetPagedByAdminScopeAsync_FiltersByCenterId()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new UserRepository(ctx);

        var u1 = User.Create("u1@example.com", "One", "User");
        u1.AssignRole(1, UserRole.Teacher);
        u1.AssignRole(2, UserRole.Student);

        var u2 = User.Create("u2@example.com", "Two", "User");
        u2.AssignRole(2, UserRole.Student);

        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetPagedByAdminScopeAsync(
            new UserListFilter(new HashSet<int> { 1, 2 }, new HashSet<int> { 1 }, null, null, null),
            new PageRequest(1, 10, null, null),
            ct);

        Assert.Single(result.Data);
        Assert.Equal("u1@example.com", result.Data[0].Email);
    }

    [Fact]
    public async Task GetPagedByAdminScopeAsync_FiltersByRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new UserRepository(ctx);

        var u1 = User.Create("u1@example.com", "One", "User");
        u1.AssignRole(1, UserRole.Teacher);

        var u2 = User.Create("u2@example.com", "Two", "User");
        u2.AssignRole(1, UserRole.Student);

        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetPagedByAdminScopeAsync(
            new UserListFilter(new HashSet<int> { 1 }, null, new HashSet<UserRole> { UserRole.Teacher }, null, null),
            new PageRequest(1, 10, null, null),
            ct);

        Assert.Single(result.Data);
        Assert.Equal("u1@example.com", result.Data[0].Email);
    }

    [Fact]
    public async Task GetPagedByAdminScopeAsync_FiltersBySearch()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new UserRepository(ctx);

        var u1 = User.Create("alice@example.com", "Alice", "Smith");
        u1.AssignRole(1, UserRole.Teacher);

        var u2 = User.Create("bob@example.com", "Bob", "Jones");
        u2.AssignRole(1, UserRole.Student);

        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetPagedByAdminScopeAsync(
            new UserListFilter(new HashSet<int> { 1 }, null, null, "alice", null),
            new PageRequest(1, 10, null, null),
            ct);

        Assert.Single(result.Data);
        Assert.Equal("alice@example.com", result.Data[0].Email);
    }

    [Fact]
    public async Task GetPagedByAdminScopeAsync_ExcludesSoftDeletedUsers()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new UserRepository(ctx);

        var u1 = User.Create("u1@example.com", "One", "User");
        u1.AssignRole(1, UserRole.Teacher);

        var u2 = User.Create("u2@example.com", "Two", "User");
        u2.AssignRole(1, UserRole.Student);
        u2.SoftDelete();

        ctx.Users.AddRange(u1, u2);
        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetPagedByAdminScopeAsync(
            new UserListFilter(new HashSet<int> { 1 }, null, null, null, null),
            new PageRequest(1, 10, null, null),
            ct);

        Assert.Single(result.Data);
        Assert.Equal("u1@example.com", result.Data[0].Email);
    }

    [Fact]
    public async Task GetPagedByAdminScopeAsync_RespectsPaging()
    {
        var ct = TestContext.Current.CancellationToken;
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var repo = new UserRepository(ctx);

        for (var i = 1; i <= 5; i++)
        {
            var user = User.Create($"user{i}@example.com", $"User{i}", "Test");
            user.AssignRole(1, UserRole.Teacher);
            ctx.Users.Add(user);
        }

        await ctx.SaveChangesAsync(ct);

        var result = await repo.GetPagedByAdminScopeAsync(
            new UserListFilter(new HashSet<int> { 1 }, null, null, null, null),
            new PageRequest(1, 2, null, null),
            ct);

        Assert.Equal(2, result.Data.Count);
        Assert.True(result.HasMoreItems);
    }
}
