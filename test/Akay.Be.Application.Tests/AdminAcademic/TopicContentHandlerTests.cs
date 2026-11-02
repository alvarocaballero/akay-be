using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.TopicAdditionalContents;
using Akay.Be.Application.Features.TopicContents;
using Akay.Be.Domain.Entities.Academic;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using Moq;

namespace Akay.Be.Application.Tests.AdminAcademic;

public class TopicContentHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetByTopic_ReturnsMappedContents()
    {
        var topic = CreateTopic(5, 10);
        var content = CreateContent(topic, 7, "notes.pdf");

        var topicContentRepository = new Mock<ITopicContentRepository>();
        topicContentRepository.Setup(x => x.GetByTopicIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);

        var handler = new GetTopicContentsQueryHandler(SuccessScope().Object,
                                                       TopicRepository(topic).Object,
                                                       topicContentRepository.Object);

        var result = await handler.Handle(new GetTopicContentsQuery(5), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var response = Assert.Single(result.Value);
        Assert.Equal(7, response.Id);
        Assert.Equal(5, response.TopicId);
        Assert.Equal("notes.pdf", response.FileName);
        Assert.Equal("application/pdf", response.ContentType);
    }

    [Fact]
    public async Task GetByTopic_WhenTopicMissing_ReturnsNotFound()
    {
        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((Topic?)null);

        var handler = new GetTopicContentsQueryHandler(SuccessScope().Object,
                                                       topicRepository.Object,
                                                       new Mock<ITopicContentRepository>().Object);

        var result = await handler.Handle(new GetTopicContentsQuery(5), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("topic.not_found", result.Error.Code);
    }

    [Fact]
    public async Task GetByTopic_WhenNoReadAccess_ReturnsForbidden()
    {
        var topic = CreateTopic(5, 10);

        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanReadSubjectContentAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Forbidden("admin.forbidden", "No tienes permisos"));

        var topicContentRepository = new Mock<ITopicContentRepository>();
        var handler = new GetTopicContentsQueryHandler(adminScope.Object,
                                                       TopicRepository(topic).Object,
                                                       topicContentRepository.Object);

        var result = await handler.Handle(new GetTopicContentsQuery(5), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("admin.forbidden", result.Error.Code);
        topicContentRepository.Verify(x => x.GetByTopicIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_RemovesContentAndSaves()
    {
        var topic = CreateTopic(5, 10);
        var content = CreateContent(topic, 7, "notes.pdf");

        var topicContentRepository = new Mock<ITopicContentRepository>();
        topicContentRepository.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(content);
        topicContentRepository.Setup(x => x.Remove(content));

        var unitOfWork = SuccessUnitOfWork();
        var handler = new DeleteTopicContentCommandHandler(SuccessScope().Object,
                                                           unitOfWork.Object,
                                                           TopicRepository(topic).Object,
                                                           topicContentRepository.Object);

        var result = await handler.Handle(new DeleteTopicContentCommand(5, 7), Ct);

        Assert.True(result.IsSuccess);
        topicContentRepository.Verify(x => x.Remove(content), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenContentBelongsToAnotherTopic_ReturnsNotFound()
    {
        var topic = CreateTopic(5, 10);
        var otherTopic = CreateTopic(6, 10);
        var content = CreateContent(otherTopic, 7, "notes.pdf");

        var topicContentRepository = new Mock<ITopicContentRepository>();
        topicContentRepository.Setup(x => x.GetByIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(content);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new DeleteTopicContentCommandHandler(SuccessScope().Object,
                                                           unitOfWork.Object,
                                                           TopicRepository(topic).Object,
                                                           topicContentRepository.Object);

        var result = await handler.Handle(new DeleteTopicContentCommand(5, 7), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("topic_content.not_found", result.Error.Code);
        topicContentRepository.Verify(x => x.Remove(It.IsAny<TopicContent>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenNoWriteAccess_ReturnsForbidden()
    {
        var topic = CreateTopic(5, 10);

        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteSubjectContentAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Forbidden("admin.forbidden", "No tienes permisos"));

        var topicContentRepository = new Mock<ITopicContentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new DeleteTopicContentCommandHandler(adminScope.Object,
                                                           unitOfWork.Object,
                                                           TopicRepository(topic).Object,
                                                           topicContentRepository.Object);

        var result = await handler.Handle(new DeleteTopicContentCommand(5, 7), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("admin.forbidden", result.Error.Code);
        topicContentRepository.Verify(x => x.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAdditionalByTopic_ReturnsMappedContents()
    {
        var topic = CreateTopic(5, 10);
        var content = CreateAdditionalContent(topic, 9, "extra.pdf");

        var additionalRepository = new Mock<ITopicAdditionalContentRepository>();
        additionalRepository.Setup(x => x.GetByTopicIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);

        var handler = new GetTopicAdditionalContentsQueryHandler(SuccessScope().Object,
                                                                 TopicRepository(topic).Object,
                                                                 additionalRepository.Object);

        var result = await handler.Handle(new GetTopicAdditionalContentsQuery(5), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var response = Assert.Single(result.Value);
        Assert.Equal(9, response.Id);
        Assert.Equal(5, response.TopicId);
        Assert.Equal("extra.pdf", response.FileName);
        Assert.Null(response.IsVisible);
    }

    [Fact]
    public async Task DeleteAdditional_RemovesContentAndSaves()
    {
        var topic = CreateTopic(5, 10);
        var content = CreateAdditionalContent(topic, 9, "extra.pdf");

        var additionalRepository = new Mock<ITopicAdditionalContentRepository>();
        additionalRepository.Setup(x => x.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(content);

        var unitOfWork = SuccessUnitOfWork();
        var handler = new DeleteTopicAdditionalContentCommandHandler(SuccessScope().Object,
                                                                     unitOfWork.Object,
                                                                     TopicRepository(topic).Object,
                                                                     additionalRepository.Object);

        var result = await handler.Handle(new DeleteTopicAdditionalContentCommand(5, 9), Ct);

        Assert.True(result.IsSuccess);
        additionalRepository.Verify(x => x.Remove(content), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAdditional_WhenContentBelongsToAnotherTopic_ReturnsNotFound()
    {
        var topic = CreateTopic(5, 10);
        var otherTopic = CreateTopic(6, 10);
        var content = CreateAdditionalContent(otherTopic, 9, "extra.pdf");

        var additionalRepository = new Mock<ITopicAdditionalContentRepository>();
        additionalRepository.Setup(x => x.GetByIdAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(content);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new DeleteTopicAdditionalContentCommandHandler(SuccessScope().Object,
                                                                     unitOfWork.Object,
                                                                     TopicRepository(topic).Object,
                                                                     additionalRepository.Object);

        var result = await handler.Handle(new DeleteTopicAdditionalContentCommand(5, 9), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("topic_additional_content.not_found", result.Error.Code);
        additionalRepository.Verify(x => x.Remove(It.IsAny<TopicAdditionalContent>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IAdminScopeService> SuccessScope()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteSubjectContentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        adminScope.Setup(x => x.EnsureCanReadSubjectContentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        return adminScope;
    }

    private static Mock<IUnitOfWork> SuccessUnitOfWork()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return unitOfWork;
    }

    private static Mock<ITopicRepository> TopicRepository(Topic topic)
    {
        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(topic.Id, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        return topicRepository;
    }

    private static Topic CreateTopic(int id, int subjectId)
    {
        var topic = Topic.Create(subjectId, "Tema");
        typeof(Topic).GetProperty(nameof(Topic.Id))!.SetValue(topic, id);
        return topic;
    }

    private static TopicContent CreateContent(Topic topic, int id, string fileName)
    {
        var content = topic.AddContent(fileName, "application/pdf");
        typeof(TopicContent).GetProperty(nameof(TopicContent.Id))!.SetValue(content, id);
        return content;
    }

    private static TopicAdditionalContent CreateAdditionalContent(Topic topic, int id, string fileName)
    {
        var content = topic.AddAdditionalContent(fileName, "application/pdf");
        typeof(TopicAdditionalContent).GetProperty(nameof(TopicAdditionalContent.Id))!.SetValue(content, id);
        return content;
    }
}
