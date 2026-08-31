using Application.Modules.Notifications.ListNotifications;
using Shared.Responses;

namespace Infrastructure.Modules.Notifications.ListNotifications;

/// <summary>
/// Coordinates the list-notifications query.
/// </summary>
public sealed class ListNotificationsHandler : IListNotificationsHandler
{
    private readonly IListNotificationsStore _store;

    public ListNotificationsHandler(IListNotificationsStore store)
    {
        _store = store;
    }

    public async Task<ApiResponse<Application.DTOs.Notification.NotificationPageDto>> HandleAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await _store.QueryAsync(query, cancellationToken);
        return ApiResponse<Application.DTOs.Notification.NotificationPageDto>.Success(page);
    }
}