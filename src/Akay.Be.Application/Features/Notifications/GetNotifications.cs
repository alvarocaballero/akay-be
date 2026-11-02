using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Notifications;

public sealed record GetNotificationsQuery : IQuery<PagedResponse<List<NotificationResponse>>>
{
    public int? Page { get; init; } = 1;
    public int? PageSize { get; init; } = 100;
}

public sealed class GetNotificationsValidator : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsValidator()
    {
        RuleFor(q => q.Page).NotNull().GreaterThan(0);
        RuleFor(q => q.PageSize).NotNull().InclusiveBetween(1, 100);
        RuleFor(q => q).Must(q => (long)(q.Page.GetValueOrDefault(1) - 1) * q.PageSize.GetValueOrDefault(100) <= int.MaxValue)
                      .WithMessage("The requested page exceeds the supported offset.");
    }
}

internal sealed class GetNotificationsHandler(IUserContext user, INotificationRepository repository)
    : IQueryHandler<GetNotificationsQuery, PagedResponse<List<NotificationResponse>>>
{
    public async ValueTask<Result<PagedResponse<List<NotificationResponse>>>> Handle(GetNotificationsQuery request,
                                                                                   CancellationToken cancellationToken)
    {
        var identity = NotificationAccess.GetUserId(user);

        if (identity.IsFailure)
            return identity.Error;

        var page = await repository.GetPagedAsync(identity.Value, request.Page!.Value,
                                                  request.PageSize!.Value, cancellationToken);
        return PagedResponse<List<NotificationResponse>>.Create(page.Data.Select(NotificationResponse.From).ToList(),
                                                                page.PageSize, page.Page, page.HasMoreItems);
    }
}
