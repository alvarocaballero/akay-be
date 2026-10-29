using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Persistence.Repositories.Academic;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.EF.Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Akay.Be.Infrastructure.Tests.Persistence;

public class TopicRepositoryTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var registration = new DbContextRegistration<ApplicationDbContext>(
            new DbContextSettings(),
            (_, _, _) => { });

        return new ApplicationDbContext(new TestUserContext(), registration, options);
    }

    [Fact]
    public async Task GetBySubjectIdAsync_FiltersBySubjectAndOrdersByDisplayOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(Guid.NewGuid().ToString());
        var repository = new TopicRepository(context);

        context.Topics.AddRange(Topic.Create(1, "Second", null, 2),
                                Topic.Create(1, "First", null, 1),
                                Topic.Create(2, "Other subject", null, 1));
        await context.SaveChangesAsync(ct);

        var result = await repository.GetBySubjectIdAsync(1, ct);

        Assert.Equal(new[] { "First", "Second" }, result.Select(topic => topic.Name));
    }

    [Fact]
    public async Task GetMaxDisplayOrderAsync_WhenSubjectHasNoTopics_ReturnsZero()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(Guid.NewGuid().ToString());
        var repository = new TopicRepository(context);

        var result = await repository.GetMaxDisplayOrderAsync(1, ct);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task GetMaxDisplayOrderAsync_ReturnsHighestOrderOfSubject()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(Guid.NewGuid().ToString());
        var repository = new TopicRepository(context);

        context.Topics.AddRange(Topic.Create(1, "One", null, 1),
                                Topic.Create(1, "Three", null, 3),
                                Topic.Create(2, "Other", null, 9));
        await context.SaveChangesAsync(ct);

        var result = await repository.GetMaxDisplayOrderAsync(1, ct);

        Assert.Equal(3, result);
    }

    private sealed class TestUserContext : IUserContext
    {
        public bool IsAuthenticated => false;
        public int UserId => 0;
        public string Name => string.Empty;
        public string Email => string.Empty;
        public IEnumerable<string> Roles => Array.Empty<string>();
    }
}
