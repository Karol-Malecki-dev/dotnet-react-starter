using Application.DTOs.Notification;
using Application.Modules.Notifications.Commands;
using Shared.Responses;

namespace Infrastructure.Modules.Notifications.Commands;

public sealed class MarkNotificationAsReadHandler : IMarkNotificationAsReadHandler
{
    private readonly IMarkNotificationAsReadStore _store;
    public MarkNotificationAsReadHandler(IMarkNotificationAsReadStore store) => _store = store;
    public async Task<ApiResponse<NotificationDto>> HandleAsync(MarkNotificationAsReadCommand command, CancellationToken cancellationToken = default)
        => await _store.MarkAsReadAsync(command.UserId, command.NotificationId, cancellationToken) is { } notification
            ? ApiResponse<NotificationDto>.Success(notification, "Notification marked as read")
            : ApiResponse<NotificationDto>.Error(404, "Notification not found");
}

public sealed class MarkAllNotificationsAsReadHandler : IMarkAllNotificationsAsReadHandler
{
    private readonly IMarkAllNotificationsAsReadStore _store;
    public MarkAllNotificationsAsReadHandler(IMarkAllNotificationsAsReadStore store) => _store = store;
    public async Task<ApiResponse<int>> HandleAsync(MarkAllNotificationsAsReadCommand command, CancellationToken cancellationToken = default)
        => ApiResponse<int>.Success(await _store.MarkAllAsReadAsync(command.UserId, cancellationToken), "Notifications marked as read");
}

public sealed class UpdateNotificationEmailPreferenceHandler : IUpdateNotificationEmailPreferenceHandler
{
    private readonly IUpdateNotificationEmailPreferenceStore _store;
    public UpdateNotificationEmailPreferenceHandler(IUpdateNotificationEmailPreferenceStore store) => _store = store;
    public async Task<ApiResponse<NotificationEmailPreferenceDto>> HandleAsync(UpdateNotificationEmailPreferenceCommand command, CancellationToken cancellationToken = default)
        => ApiResponse<NotificationEmailPreferenceDto>.Success(
            await _store.UpdateAsync(command.UserId, command.IsEmailEnabled, command.IsTaskDeadlineReminderEmailEnabled, cancellationToken),
            "Notification email preference updated");
}