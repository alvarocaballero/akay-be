using System.Text.Json;
using Akay.Be.Application;
using Akay.Be.Application.Features.TopicContents;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Entities.Organization;
using Akay.Be.Domain.Enums;
using Akay.Be.Infrastructure;
using Akay.Be.Infrastructure.Persistence.Context;
using Akay.Be.Infrastructure.Persistence.Repositories.Academic;
using Akay.Be.Infrastructure.Tests.Notifications;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Messaging;
using Akay.To.Core.Application.ApplicationSettings;
using Akay.To.EF.Domain.Outbox;
using Akay.To.EF.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Transport.InMem;

namespace Akay.Be.Infrastructure.Tests.Persistence;

public sealed class TopicConversionPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task UploadAndOutboxCommitAndRollbackTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var seed = await postgres.CreateServicesAsync();
        await using var seedScope = seed.CreateAsyncScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = new ApplicationSettings
        {
            AzureSignalRSettings = new(),
            MessagingSettings = new MessagingSettings
            {
                Transport = MessagingTransportNames.InMemory,
                InputQueueName = "topic-outbox-backend",
                Routes = { [typeof(ConversionRequest).FullName!] = "convert-to-markdown" }
            }
        };
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
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var center = Center.Create("Center", "CTR");
        db.Centers.Add(center);
        await db.SaveChangesAsync(ct);
        var subject = Subject.Create("Subject", null, [center.Id]);
        db.Subjects.Add(subject);
        await db.SaveChangesAsync(ct);
        var topic = Topic.Create(subject.Id, "Topic");
        db.Topics.Add(topic);
        await db.SaveChangesAsync(ct);
        var repository = new TopicRepository(db);
        var first = repository.AddContent(topic, "one.pdf", "application/pdf");
        var second = repository.AddContent(topic, "two.pdf", "application/pdf");
        var additional = repository.AddAdditionalContent(topic, "extra.pdf", "application/pdf");
        await using (var beforeSaveScope = provider.CreateAsyncScope())
        {
            var beforeSave = beforeSaveScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await beforeSave.TopicContents.ToListAsync(ct));
            Assert.Empty(await beforeSave.TopicAdditionalContents.ToListAsync(ct));
        }
        await db.SaveChangesAsync(ct);
        Assert.True(first.Id > 0);
        Assert.Equal("12", first.CreatedBy);
        Assert.True(second.Id > 0);
        Assert.True(additional.Id > 0);
        Assert.Equal(topic.Id, first.TopicId);
        Assert.Equal(topic.Id, additional.TopicId);
        await using (var afterSaveScope = provider.CreateAsyncScope())
        {
            var afterSave = afterSaveScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(2, await afterSave.TopicContents.CountAsync(x => x.TopicId == topic.Id, ct));
            Assert.Equal(additional.Id, await afterSave.TopicAdditionalContents.Where(x => x.TopicId == topic.Id).Select(x => x.Id).SingleAsync(ct));
        }
        var source = $"https://example.test/topic-content/subjects/{subject.Id}/topics/{topic.Id}/sources/{Guid.NewGuid():D}.pdf";

        topic.CompleteContentUpload(first, source, 12);
        await db.SaveChangesAsync(ct);
        var row = await db.Set<OutboxMessage>().SingleAsync(ct);
        var command = JsonSerializer.Deserialize<ConversionRequest>(row.Content)!;
        Assert.IsAssignableFrom<ICommandMessage>(command);
        Assert.Equal(first.RequestId, command.requestId);
        Assert.Equal(first.Id, command.payload.topicContentId);
        Assert.Equal(12, command.payload.userId);

        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            topic.CompleteContentUpload(second, source, 12);
            await db.SaveChangesAsync(ct);
            await transaction.RollbackAsync(ct);
        }
        db.ChangeTracker.Clear();
        Assert.Equal(ContentProcessingState.Pending, await db.TopicContents.Where(x => x.Id == first.Id).Select(x => x.ProcessingState).SingleAsync(ct));
        var untouched = await db.TopicContents.SingleAsync(x => x.Id == second.Id, ct);
        Assert.Equal(ContentProcessingState.Uploading, untouched.ProcessingState);
        Assert.Null(untouched.RequestId);
        Assert.Single(await db.Set<OutboxMessage>().ToListAsync(ct));

        var network = provider.GetRequiredService<InMemNetwork>();
        network.CreateQueue("convert-to-markdown");
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var processor = provider.GetRequiredService<OutboxProcessor<ApplicationDbContext>>().RunAsync(stopping.Token);
        try
        {
            while (network.Count("convert-to-markdown") == 0 && !stopping.IsCancellationRequested)
                await Task.Delay(50, ct);
            var delivered = Assert.Single(network.GetMessages("convert-to-markdown"));
            using var body = JsonDocument.Parse(delivered.Body);
            Assert.Equal(first.RequestId, body.RootElement.GetProperty("requestId").GetGuid());
            Assert.Equal(row.Id.ToString(), delivered.Headers["outbox-id"]);
            Assert.Empty(network.GetMessages("topic-outbox-backend"));
        }
        finally
        {
            await stopping.CancelAsync();
            await processor;
        }
    }

    [Fact]
    public async Task CompletionReadsCommittedStatesAndConcurrentWritersReload()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var services = await postgres.CreateServicesAsync();
        await using var setupScope = services.CreateAsyncScope();
        var setup = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var center = Center.Create("Center", "CTR");
        setup.Centers.Add(center);
        await setup.SaveChangesAsync(ct);
        var subject = Subject.Create("Subject", null, [center.Id]);
        setup.Subjects.Add(subject);
        await setup.SaveChangesAsync(ct);
        var topic = Topic.Create(subject.Id, "Topic");
        var first = topic.AddContent("one.pdf", "application/pdf");
        var second = topic.AddContent("two.pdf", "application/pdf");
        var readyTopic = Topic.Create(subject.Id, "Ready topic");
        var readyContent = readyTopic.AddContent("ready.pdf", "application/pdf");
        var failedUploadTopic = Topic.Create(subject.Id, "Failed upload topic");
        var failedUploadContent = failedUploadTopic.AddContent("upload.pdf", "application/pdf");
        setup.Topics.Add(topic);
        setup.Topics.AddRange(readyTopic, failedUploadTopic);
        await setup.SaveChangesAsync(ct);
        topic.CompleteContentUpload(first, "https://example.test/one", 12);
        topic.CompleteContentUpload(second, "https://example.test/two", 12);
        readyTopic.CompleteContentUpload(readyContent, "https://example.test/ready", 12);
        readyTopic.CompleteContentConversion(readyContent, readyContent.RequestId!.Value, "https://example.test/ready.md");
        failedUploadTopic.FailContentUpload(failedUploadContent, "upload failed");
        await setup.SaveChangesAsync(ct);

        await using var firstScope = services.CreateAsyncScope();
        await using var secondScope = services.CreateAsyncScope();
        var firstRepository = new TopicRepository(firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var secondRepository = new TopicRepository(secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        Assert.False((await firstRepository.GetContentCompletionAsync(readyTopic.Id, ct))!.HasErrors);
        Assert.Null(await firstRepository.GetContentCompletionAsync(failedUploadTopic.Id, ct));
        var a = (await firstRepository.GetWithContentsAsync(topic.Id, ct))!;
        var b = (await secondRepository.GetWithContentsAsync(topic.Id, ct))!;
        var firstA = a.Contents.Single(x => x.Id == first.Id);
        var firstB = b.Contents.Single(x => x.Id == first.Id);
        a.CompleteContentConversion(firstA, firstA.RequestId!.Value, "https://example.test/one.md");
        b.FailContentConversion(firstB, firstB.RequestId!.Value, "error");
        Assert.True(await firstRepository.TrySaveConversionAsync(ct));
        Assert.False(await secondRepository.TrySaveConversionAsync(ct));
        Assert.Equal(ContentProcessingState.Ready, (await secondRepository.GetWithContentsAsync(topic.Id, ct))!.Contents.Single(x => x.Id == first.Id).ProcessingState);
        Assert.Null(await secondRepository.GetContentCompletionAsync(topic.Id, ct));
        var secondA = a.Contents.Single(x => x.Id == second.Id);
        a.FailContentConversion(secondA, secondA.RequestId!.Value, "error");
        Assert.True(await firstRepository.TrySaveConversionAsync(ct));
        Assert.True((await secondRepository.GetContentCompletionAsync(topic.Id, ct))!.HasErrors);
    }
}
