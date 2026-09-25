using Application.Modules.Notifications.ListNotifications;
using MediatR;
using Shared.Responses;

namespace Infrastructure.Modules.Notifications.ListNotifications;

/// <summary>
/// Coordinates the list-notifications query.
/// </summary>
public sealed class ListNotificationsHandler
    : IRequestHandler<ListNotificationsQuery, ApiResponse<Application.DTOs.Notification.NotificationPageDto>>
{
    private readonly IListNotificationsStore _store;

    public ListNotificationsHandler(IListNotificationsStore store)
    {
        _store = store;
    }

    public async Task<ApiResponse<Application.DTOs.Notification.NotificationPageDto>> Handle(
        ListNotificationsQuery query,
        CancellationToken cancellationToken)
    {
        var page = await _store.QueryAsync(query, cancellationToken);
        return ApiResponse<Application.DTOs.Notification.NotificationPageDto>.Success(page);
    }
}