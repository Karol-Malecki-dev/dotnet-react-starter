using Application.DTOs.Notification;
using Application.Interfaces;
using Shared.Responses;

namespace Application.Modules.Notifications.Commands;

public sealed record MarkNotificationAsReadCommand(Guid UserId, Guid NotificationId);
public sealed record MarkAllNotificationsAsReadCommand(Guid UserId);
public sealed record UpdateNotificationEmailPreferenceCommand(Guid UserId, bool? IsEmailEnabled, bool? IsTaskDeadlineReminderEmailEnabled);

public interface IMarkNotificationAsReadHandler
{
    Task<ApiResponse<NotificationDto>> HandleAsync(MarkNotificationAsReadCommand command, CancellationToken cancellationToken = default);
}

public interface IMarkAllNotificationsAsReadHandler
{
    Task<ApiResponse<int>> HandleAsync(MarkAllNotificationsAsReadCommand command, CancellationToken cancellationToken = default);
}

public interface IUpdateNotificationEmailPreferenceHandler
{
    Task<ApiResponse<NotificationEmailPreferenceDto>> HandleAsync(UpdateNotificationEmailPreferenceCommand command, CancellationToken cancellationToken = default);
}