using Akay.Be.Application.Abstractions.Persistence.Repositories.Academic;
using Akay.Be.Application.Abstractions.Services;
using Akay.Be.Application.Features.Topics;
using Akay.Be.Domain.Entities.Academic;
using Akay.Be.Domain.Enums;
using Akay.To.Core.Application.Abstractions.Persistence;
using Akay.To.Core.Application.Results;
using Moq;

namespace Akay.Be.Application.Tests.AdminAcademic;

public class TopicHandlerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_FirstTopic_UsesDisplayOrderOne()
    {
        var adminScope = SuccessScope();
        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetMaxDisplayOrderAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        Topic? added = null;
        topicRepository.Setup(x => x.Add(It.IsAny<Topic>())).Callback<Topic>(topic => added = topic);

        var handler = new CreateTopicCommandHandler(adminScope.Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new CreateTopicCommand(10, "Tema", null), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(1, added.DisplayOrder);
        Assert.Equal(TopicProcessingState.Draft, added.ProcessingState);
        Assert.Equal(TopicIndexingState.NotIndexed, added.IndexingState);
        Assert.False(added.IsActive);
    }

    [Fact]
    public async Task Create_WithExistingTopics_UsesLastPlusOne()
    {
        var adminScope = SuccessScope();
        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetMaxDisplayOrderAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(4);

        Topic? added = null;
        topicRepository.Setup(x => x.Add(It.IsAny<Topic>())).Callback<Topic>(topic => added = topic);

        var handler = new CreateTopicCommandHandler(adminScope.Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new CreateTopicCommand(10, "Tema", null), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(5, added.DisplayOrder);
    }

    [Fact]
    public async Task Create_WithDisplayOrderInMiddle_ShiftsFollowingTopics()
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two"),
            CreateTopic(3, 10, 3, "Three")
        };

        Topic? added = null;
        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetMaxDisplayOrderAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);
        topicRepository.Setup(x => x.Add(It.IsAny<Topic>())).Callback<Topic>(topic => added = topic);

        var handler = new CreateTopicCommandHandler(SuccessScope().Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new CreateTopicCommand(10, "New", null, 2), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(2, added.DisplayOrder);
        Assert.Equal(1, topics[0].DisplayOrder);
        Assert.Equal(3, topics[1].DisplayOrder);
        Assert.Equal(4, topics[2].DisplayOrder);
    }

    [Fact]
    public async Task Create_WithDisplayOrderAtEnd_DoesNotShiftTopics()
    {
        Topic? added = null;
        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetMaxDisplayOrderAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(3);
        topicRepository.Setup(x => x.Add(It.IsAny<Topic>())).Callback<Topic>(topic => added = topic);

        var handler = new CreateTopicCommandHandler(SuccessScope().Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new CreateTopicCommand(10, "New", null, 4), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(4, added.DisplayOrder);
        topicRepository.Verify(x => x.GetBySubjectIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Create_WithInvalidDisplayOrder_ReturnsValidationError(int displayOrder)
    {
        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetMaxDisplayOrderAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(3);

        var handler = new CreateTopicCommandHandler(SuccessScope().Object, new Mock<IUnitOfWork>().Object, topicRepository.Object);

        var result = await handler.Handle(new CreateTopicCommand(10, "New", null, displayOrder), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("topic.invalid_display_order", result.Error.Code);
        topicRepository.Verify(x => x.Add(It.IsAny<Topic>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenNotSubjectAdmin_ReturnsForbidden()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanWriteSubjectContentAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Forbidden("admin.forbidden", "No tienes permisos"));

        var topicRepository = new Mock<ITopicRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var handler = new CreateTopicCommandHandler(adminScope.Object, unitOfWork.Object, topicRepository.Object);

        var result = await handler.Handle(new CreateTopicCommand(10, "Tema", null), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("admin.forbidden", result.Error.Code);
        topicRepository.Verify(x => x.Add(It.IsAny<Topic>()), Times.Never);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ChangesNameDescriptionAndActivation_PreservingOtherFields()
    {
        var topic = CreateTopic(5, 10, 3, "Old");
        topic.ChangeProcessingState(TopicProcessingState.Ready);
        topic.ChangeIndexingState(TopicIndexingState.Indexed);
        topic.ChangeActivation(false);

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic);

        var handler = new UpdateTopicCommandHandler(SuccessScope().Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new UpdateTopicCommand(5, "New", "Description", true), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("New", topic.Name);
        Assert.Equal("Description", topic.Description);
        Assert.Equal(3, topic.DisplayOrder);
        Assert.Equal(TopicProcessingState.Ready, topic.ProcessingState);
        Assert.Equal(TopicIndexingState.Indexed, topic.IndexingState);
        Assert.True(topic.IsActive);
        topicRepository.Verify(x => x.GetBySubjectIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_WithDisplayOrderInMiddle_ReordersTopics()
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two"),
            CreateTopic(3, 10, 3, "Three"),
            CreateTopic(4, 10, 4, "Four")
        };

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(topics[3]);
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);

        var handler = new UpdateTopicCommandHandler(SuccessScope().Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new UpdateTopicCommand(4, "Updated", "Description", true, 2), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, topics[0].DisplayOrder);
        Assert.Equal(3, topics[1].DisplayOrder);
        Assert.Equal(4, topics[2].DisplayOrder);
        Assert.Equal(2, topics[3].DisplayOrder);
        Assert.Equal("Updated", topics[3].Name);
        Assert.True(topics[3].IsActive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Update_WithInvalidDisplayOrder_ReturnsValidationErrorWithoutChanges(int displayOrder)
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two"),
            CreateTopic(3, 10, 3, "Three"),
            CreateTopic(4, 10, 4, "Four")
        };

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(topics[3]);
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new UpdateTopicCommandHandler(SuccessScope().Object, unitOfWork.Object, topicRepository.Object);

        var result = await handler.Handle(new UpdateTopicCommand(4, "Updated", null, true, displayOrder), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("topic.invalid_display_order", result.Error.Code);
        Assert.Equal("Four", topics[3].Name);
        Assert.Equal(4, topics[3].DisplayOrder);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_CanDeactivateTopic()
    {
        var topic = CreateTopic(5, 10, 1, "Topic");
        topic.ChangeActivation(true);

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(topic);

        var handler = new UpdateTopicCommandHandler(SuccessScope().Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new UpdateTopicCommand(5, "Topic", null, false), Ct);

        Assert.True(result.IsSuccess);
        Assert.False(topic.IsActive);
    }

    [Fact]
    public async Task Update_WhenTopicDoesNotExist_ReturnsNotFound()
    {
        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((Topic?)null);

        var handler = new UpdateTopicCommandHandler(SuccessScope().Object, new Mock<IUnitOfWork>().Object, topicRepository.Object);

        var result = await handler.Handle(new UpdateTopicCommand(5, "New", null, false), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("topic.not_found", result.Error.Code);
    }

    [Fact]
    public async Task Delete_SoftDeletesAndCompactsRemainingOrders()
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two"),
            CreateTopic(3, 10, 3, "Three"),
            CreateTopic(4, 10, 4, "Four")
        };

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(topics[1]);
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);

        var unitOfWork = SuccessUnitOfWork();
        var handler = new DeleteTopicCommandHandler(SuccessScope().Object, unitOfWork.Object, topicRepository.Object);

        var result = await handler.Handle(new DeleteTopicCommand(2), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, topics[0].DisplayOrder);
        Assert.Equal(2, topics[2].DisplayOrder);
        Assert.Equal(3, topics[3].DisplayOrder);
        topicRepository.Verify(x => x.Remove(topics[1]), Times.Once);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_OnlyShiftsTopicsAboveDeletedOrder()
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two"),
            CreateTopic(3, 10, 5, "Five")
        };

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(topics[1]);
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);

        var handler = new DeleteTopicCommandHandler(SuccessScope().Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new DeleteTopicCommand(2), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, topics[0].DisplayOrder);
        Assert.Equal(4, topics[2].DisplayOrder);
    }

    [Fact]
    public async Task Reorder_FromFourToTwo_ShiftsFollowingTopics()
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two"),
            CreateTopic(3, 10, 3, "Three"),
            CreateTopic(4, 10, 4, "Four")
        };

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(topics[3]);
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);

        var handler = new ReorderTopicCommandHandler(SuccessScope().Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new ReorderTopicCommand(4, 2), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, topics[0].DisplayOrder);
        Assert.Equal(3, topics[1].DisplayOrder);
        Assert.Equal(4, topics[2].DisplayOrder);
        Assert.Equal(2, topics[3].DisplayOrder);
    }

    [Fact]
    public async Task Reorder_FromTwoToFour_ShiftsPreviousTopics()
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two"),
            CreateTopic(3, 10, 3, "Three"),
            CreateTopic(4, 10, 4, "Four")
        };

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(topics[1]);
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);

        var handler = new ReorderTopicCommandHandler(SuccessScope().Object, SuccessUnitOfWork().Object, topicRepository.Object);

        var result = await handler.Handle(new ReorderTopicCommand(2, 4), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, topics[0].DisplayOrder);
        Assert.Equal(4, topics[1].DisplayOrder);
        Assert.Equal(2, topics[2].DisplayOrder);
        Assert.Equal(3, topics[3].DisplayOrder);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Reorder_OutOfRange_ReturnsValidationError(int displayOrder)
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two"),
            CreateTopic(3, 10, 3, "Three"),
            CreateTopic(4, 10, 4, "Four")
        };

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(topics[3]);
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new ReorderTopicCommandHandler(SuccessScope().Object, unitOfWork.Object, topicRepository.Object);

        var result = await handler.Handle(new ReorderTopicCommand(4, displayOrder), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("topic.invalid_display_order", result.Error.Code);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Reorder_ToCurrentPosition_DoesNothing()
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two")
        };

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(topics[1]);
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);

        var unitOfWork = new Mock<IUnitOfWork>();
        var handler = new ReorderTopicCommandHandler(SuccessScope().Object, unitOfWork.Object, topicRepository.Object);

        var result = await handler.Handle(new ReorderTopicCommand(2, 2), Ct);

        Assert.True(result.IsSuccess);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetBySubject_ReturnsTopicsOrderedWithAllFields()
    {
        var topics = new List<Topic>
        {
            CreateTopic(1, 10, 1, "One"),
            CreateTopic(2, 10, 2, "Two")
        };

        var topicRepository = new Mock<ITopicRepository>();
        topicRepository.Setup(x => x.GetBySubjectIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(topics);

        var handler = new GetTopicsBySubjectQueryHandler(SuccessScope().Object, topicRepository.Object);

        var result = await handler.Handle(new GetTopicsBySubjectQuery(10), Ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        Assert.Equal(new[] { 1, 2 }, result.Value.Select(topic => topic.DisplayOrder));
        Assert.All(result.Value, topic => Assert.Equal(10, topic.SubjectId));
    }

    [Fact]
    public async Task GetBySubject_WhenNoReadAccess_ReturnsForbidden()
    {
        var adminScope = new Mock<IAdminScopeService>();
        adminScope.Setup(x => x.EnsureCanReadSubjectContentAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Error.Forbidden("admin.forbidden", "No tienes permisos"));

        var topicRepository = new Mock<ITopicRepository>();
        var handler = new GetTopicsBySubjectQueryHandler(adminScope.Object, topicRepository.Object);

        var result = await handler.Handle(new GetTopicsBySubjectQuery(10), Ct);

        Assert.True(result.IsFailure);
        Assert.Equal("admin.forbidden", result.Error.Code);
        topicRepository.Verify(x => x.GetBySubjectIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private static Topic CreateTopic(int id, int subjectId, int displayOrder, string name)
    {
        var topic = Topic.Create(subjectId, name, null, displayOrder);
        typeof(Topic).GetProperty(nameof(Topic.Id))!.SetValue(topic, id);
        return topic;
    }
}
