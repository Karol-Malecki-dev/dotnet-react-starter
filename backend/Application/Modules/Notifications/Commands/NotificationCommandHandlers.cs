using Application.DTOs.Notification;
using MediatR;
using Shared.Responses;

namespace Application.Modules.Notifications.Commands;

public sealed record MarkNotificationAsReadCommand(Guid UserId, Guid NotificationId)
    : IRequest<ApiResponse<NotificationDto>>;

public sealed record MarkAllNotificationsAsReadCommand(Guid UserId)
    : IRequest<ApiResponse<int>>;

public sealed record UpdateNotificationEmailPreferenceCommand(
    Guid UserId,
    bool? IsEmailEnabled,
    bool? IsTaskDeadlineReminderEmailEnabled)
    : IRequest<ApiResponse<NotificationEmailPreferenceDto>>;

public interface IMarkNotificationAsReadStore
{
    Task<NotificationDto?> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
}

public interface IMarkAllNotificationsAsReadStore
{
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
}

public interface IUpdateNotificationEmailPreferenceStore
{
    Task<NotificationEmailPreferenceDto> UpdateAsync(
        Guid userId,
        bool? isEmailEnabled,
        bool? isTaskDeadlineReminderEmailEnabled,
        CancellationToken cancellationToken = default);
}