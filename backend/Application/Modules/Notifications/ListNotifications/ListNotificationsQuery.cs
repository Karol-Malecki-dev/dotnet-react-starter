using Application.DTOs.Notification;
using Shared.Responses;

namespace Application.Modules.Notifications.ListNotifications;

/// <summary>
/// Requests a page of notifications belonging to the authenticated user.
/// </summary>
public sealed record ListNotificationsQuery(
    Guid UserId,
    int PageNumber = 1,
    int PageSize = 20,
    bool UnreadOnly = false);

/// <summary>
/// Executes the list-notifications use case.
/// </summary>
public interface IListNotificationsHandler
{
    Task<ApiResponse<NotificationPageDto>> HandleAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the notification projection required by the list-notifications slice.
/// </summary>
public interface IListNotificationsStore
{
    Task<NotificationPageDto> QueryAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken = default);
}