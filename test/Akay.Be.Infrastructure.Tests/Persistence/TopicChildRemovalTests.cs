using Akay.Be.Application;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Organization;
using Akay.Be.Infrastructure;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Persistence.Repositories.Academic;
using Akay.Be.Infrastructure.Tests.Notifications;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.ApplicationSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Akay.Be.Infrastructure.Tests.Persistence;

public sealed class TopicChildRemovalTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task RepositoryRemovesOnlyOwnedChildrenAndPreservesSoftDeletedRows()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var seed = await postgres.CreateServicesAsync();
        await using var seedScope = seed.CreateAsyncScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = new ApplicationSettings { AzureSignalRSettings = new() };
        settings.DbContextSettings["ApplicationDbContext"] = new DbContextSettings
        {
            ConnectionString = seedDb.Database.GetConnectionString(),
            Interceptors = new() { TenantAndOwnership = false }
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(seedScope.ServiceProvider.GetRequiredService<IUserContext>());
        services.AddInfrastructureServices(settings).AddApplicationServices(settings);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var setupScope = provider.CreateAsyncScope();
        var setup = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var center = Center.Create("Center", "CTR");
        setup.Centers.Add(center);
        await setup.SaveChangesAsync(ct);
        var subject = Subject.Create("Subject", null, [center.Id]);
        setup.Subjects.Add(subject);
        await setup.SaveChangesAsync(ct);
        var topic = Topic.Create(subject.Id, "Topic");
        var content = topic.AddContent("file.pdf", "application/pdf");
        var additional = topic.AddAdditionalContent("additional.pdf", "application/pdf");
        var other = Topic.Create(subject.Id, "Other", displayOrder: 2);
        var foreignContent = other.AddContent("foreign.pdf", "application/pdf");
        var foreignAdditional = other.AddAdditionalContent("foreign-extra.pdf", "application/pdf");
        setup.Topics.AddRange(topic, other);
        await setup.SaveChangesAsync(ct);

        await using var staleScope = provider.CreateAsyncScope();
        var staleDb = staleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staleRepository = new TopicRepository(staleDb);
        var staleTopic = (await staleRepository.GetWithContentsAsync(topic.Id, ct))!;
        var staleContent = staleTopic.Contents.Single();

        await using var writeScope = provider.CreateAsyncScope();
        var db = writeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repository = new TopicRepository(db);
        var loaded = (await repository.GetWithContentsAsync(topic.Id, ct))!;
        loaded = (await repository.GetWithAdditionalContentsAsync(topic.Id, ct))!;
        var loadedOther = (await repository.GetWithContentsAsync(other.Id, ct))!;
        loadedOther = (await repository.GetWithAdditionalContentsAsync(other.Id, ct))!;

        Assert.Throws<InvalidOperationException>(() => repository.RemoveContent(loaded, loadedOther.Contents.Single()));
        Assert.Throws<InvalidOperationException>(() => repository.RemoveAdditionalContent(loaded, loadedOther.AdditionalContents.Single()));
        Assert.Equal(EntityState.Unchanged, db.Entry(loadedOther.Contents.Single()).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(loadedOther.AdditionalContents.Single()).State);

        repository.RemoveContent(loaded, loaded.Contents.Single());
        repository.RemoveAdditionalContent(loaded, loaded.AdditionalContents.Single());
        await using (var beforeSaveScope = provider.CreateAsyncScope())
        {
            var beforeSave = beforeSaveScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await beforeSave.TopicContents.AnyAsync(x => x.Id == content.Id, ct));
            Assert.True(await beforeSave.TopicAdditionalContents.AnyAsync(x => x.Id == additional.Id, ct));
        }
        await db.SaveChangesAsync(ct);

        staleTopic.FailContentUpload(staleContent, "The upload failed.");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleDb.SaveChangesAsync(ct));

        await using var readScope = provider.CreateAsyncScope();
        var read = readScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await read.TopicContents.IgnoreQueryFilters().SingleAsync(x => x.Id == content.Id, ct);
        var deletedAdditional = await read.TopicAdditionalContents.IgnoreQueryFilters().SingleAsync(x => x.Id == additional.Id, ct);
        Assert.NotNull(deleted.DeletedAt);
        Assert.NotNull(deletedAdditional.DeletedAt);
        Assert.Equal(topic.Id, deleted.TopicId);
        Assert.Equal(topic.Id, deletedAdditional.TopicId);
        Assert.False(await read.TopicContents.AnyAsync(x => x.Id == content.Id, ct));
        Assert.False(await read.TopicAdditionalContents.AnyAsync(x => x.Id == additional.Id, ct));
        Assert.True(await read.TopicContents.AnyAsync(x => x.Id == foreignContent.Id, ct));
        Assert.True(await read.TopicAdditionalContents.AnyAsync(x => x.Id == foreignAdditional.Id, ct));
    }
}
