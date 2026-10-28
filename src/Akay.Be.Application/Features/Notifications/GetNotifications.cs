using Akay.Be.Application.Abstractions.Persistence.Repositories.Notification;
using Akay.To.Core.Application.Abstractions.Contexts;
using Akay.To.Core.Application.Abstractions.Mediator;
using Akay.To.Core.Application.Responses;
using Akay.To.Core.Application.Results;
using FluentValidation;

namespace Akay.Be.Application.Features.Notifications;

public sealed record GetNotificationsQuery : PagedQuery<List<NotificationResponse>>
{
    public GetNotificationsQuery() => IsAscending = false;
    public bool? IsRead { get; init; }
}

public sealed class GetNotificationsValidator : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsValidator()
    {
        RuleFor(q => q.Page).NotNull().GreaterThan(0);
        RuleFor(q => q.PageSize).NotNull().InclusiveBetween(1, 100);
        RuleFor(q => q).Must(q => (long)(q.Page.GetValueOrDefault(1) - 1) * q.PageSize.GetValueOrDefault(100) <= int.MaxValue)
                      .WithMessage("The requested page exceeds the supported offset.");
        RuleFor(q => q.SortBy).Must(value => string.IsNullOrEmpty(value) || value.Equals("CreatedAt", StringComparison.OrdinalIgnoreCase))
                             .WithMessage("Notifications are ordered by CreatedAt descending.");
        RuleFor(q => q.IsAscending).Must(value => value is not true)
                                  .WithMessage("Notifications are ordered by CreatedAt descending.");
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

        var page = await repository.GetPagedAsync(identity.Value, request.IsRead, request.Page!.Value,
                                                  request.PageSize!.Value, cancellationToken);
        return PagedResponse<List<NotificationResponse>>.Create(page.Data.Select(NotificationResponse.From).ToList(),
                                                                page.PageSize, page.Page, page.HasMoreItems);
    }
}
