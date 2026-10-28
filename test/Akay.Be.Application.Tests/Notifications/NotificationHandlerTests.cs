using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.Be.Application.Features.Notifications;
using Akay.Be.Domain.Entities.Notification;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Responses;
using Moq;

namespace Akay.Be.Application.Tests.Notifications;

public sealed class NotificationHandlerTests
{
    private readonly Mock<INotificationRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IUserContext> user = new();

    public NotificationHandlerTests()
    {
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.UserId).Returns(12);
    }

    [Fact]
    public async Task List_UsesAuthenticatedIdentityAndMapsToResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var notification = PersistentNotification.Create(12, "completed", "Title", "Message", null, "{\"value\":1}", DateTimeOffset.UtcNow);
        repository.Setup(r => r.GetPagedAsync(12, false, 2, 5, ct))
                  .ReturnsAsync(PagedResponse<List<PersistentNotification>>.Create([notification], 5, 2, false));

        var result = await new GetNotificationsHandler(user.Object, repository.Object)
            .Handle(new GetNotificationsQuery { Page = 2, PageSize = 5, IsRead = false }, ct);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var response = Assert.Single(result.Value.Data);
        Assert.Equal(notification.Id, response.Id);
        Assert.Equal(1, response.Payload!.Value.GetProperty("value").GetInt32());
        Assert.Equal(2, result.Value.Page);
        Assert.Null(response.Url);
        repository.VerifyAll();
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotReadOrMutate()
    {
        user.SetupGet(u => u.IsAuthenticated).Returns(false);
        var ct = TestContext.Current.CancellationToken;
        Assert.True((await new GetNotificationsHandler(user.Object, repository.Object).Handle(new(), ct)).IsFailure);
        Assert.True((await new GetUnreadCountHandler(user.Object, repository.Object).Handle(new(), ct)).IsFailure);
        Assert.True((await new MarkNotificationReadHandler(user.Object, repository.Object).Handle(new(123), ct)).IsFailure);
        Assert.True((await new MarkAllNotificationsReadHandler(user.Object, repository.Object).Handle(new(), ct)).IsFailure);
        Assert.True((await new DeleteNotificationHandler(user.Object, repository.Object).Handle(new(123), ct)).IsFailure);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadAndDelete_PassUserScope_AndReturnUniformNotFound(bool exists)
    {
        var ct = TestContext.Current.CancellationToken;
        var id = 123;
        repository.Setup(r => r.MarkReadAsync(12, id, It.IsAny<DateTimeOffset>(), ct)).ReturnsAsync(exists);
        repository.Setup(r => r.DeleteForUserAsync(12, id, ct)).ReturnsAsync(exists);

        var read = await new MarkNotificationReadHandler(user.Object, repository.Object).Handle(new(id), ct);
        var delete = await new DeleteNotificationHandler(user.Object, repository.Object).Handle(new(id), ct);

        Assert.Equal(exists, read.IsSuccess);
        Assert.Equal(exists, delete.IsSuccess);
        if (!exists)
            Assert.Equal(read.Error, delete.Error);
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void List_RejectsInvalidOrOverflowingPagination(int page, int pageSize) =>
        Assert.False(new GetNotificationsValidator().Validate(new GetNotificationsQuery { Page = page, PageSize = pageSize }).IsValid);
}
