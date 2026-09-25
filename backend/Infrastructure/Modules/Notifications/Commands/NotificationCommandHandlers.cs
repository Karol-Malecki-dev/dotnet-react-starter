using Application.DTOs.Notification;
using Application.Modules.Notifications.Commands;
using MediatR;
using Shared.Responses;

namespace Infrastructure.Modules.Notifications.Commands;

public sealed class MarkNotificationAsReadHandler
    : IRequestHandler<MarkNotificationAsReadCommand, ApiResponse<NotificationDto>>
{
    private readonly IMarkNotificationAsReadStore _store;
    public MarkNotificationAsReadHandler(IMarkNotificationAsReadStore store) => _store = store;
    public async Task<ApiResponse<NotificationDto>> Handle(
        MarkNotificationAsReadCommand command,
        CancellationToken cancellationToken)
        => await _store.MarkAsReadAsync(command.UserId, command.NotificationId, cancellationToken) is { } notification
            ? ApiResponse<NotificationDto>.Success(notification, "Notification marked as read")
            : ApiResponse<NotificationDto>.Error(404, "Notification not found");
}

public sealed class MarkAllNotificationsAsReadHandler
    : IRequestHandler<MarkAllNotificationsAsReadCommand, ApiResponse<int>>
{
    private readonly IMarkAllNotificationsAsReadStore _store;
    public MarkAllNotificationsAsReadHandler(IMarkAllNotificationsAsReadStore store) => _store = store;
    public async Task<ApiResponse<int>> Handle(
        MarkAllNotificationsAsReadCommand command,
        CancellationToken cancellationToken)
        => ApiResponse<int>.Success(await _store.MarkAllAsReadAsync(command.UserId, cancellationToken), "Notifications marked as read");
}

public sealed class UpdateNotificationEmailPreferenceHandler
    : IRequestHandler<UpdateNotificationEmailPreferenceCommand, ApiResponse<NotificationEmailPreferenceDto>>
{
    private readonly IUpdateNotificationEmailPreferenceStore _store;
    public UpdateNotificationEmailPreferenceHandler(IUpdateNotificationEmailPreferenceStore store) => _store = store;
    public async Task<ApiResponse<NotificationEmailPreferenceDto>> Handle(
        UpdateNotificationEmailPreferenceCommand command,
        CancellationToken cancellationToken)
        => ApiResponse<NotificationEmailPreferenceDto>.Success(
            await _store.UpdateAsync(command.UserId, command.IsEmailEnabled, command.IsTaskDeadlineReminderEmailEnabled, cancellationToken),
            "Notification email preference updated");
}