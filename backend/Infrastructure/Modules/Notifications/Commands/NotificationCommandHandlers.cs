using Application.DTOs.Notification;
using Application.Interfaces;
using Application.Modules.Notifications.Commands;
using Shared.Responses;

namespace Infrastructure.Modules.Notifications.Commands;

public sealed class MarkNotificationAsReadHandler : IMarkNotificationAsReadHandler
{
    private readonly INotificationService _service;
    public MarkNotificationAsReadHandler(INotificationService service) => _service = service;
    public Task<ApiResponse<NotificationDto>> HandleAsync(MarkNotificationAsReadCommand command, CancellationToken cancellationToken = default)
        => _service.MarkAsReadAsync(command.UserId, command.NotificationId, cancellationToken);
}

public sealed class MarkAllNotificationsAsReadHandler : IMarkAllNotificationsAsReadHandler
{
    private readonly INotificationService _service;
    public MarkAllNotificationsAsReadHandler(INotificationService service) => _service = service;
    public Task<ApiResponse<int>> HandleAsync(MarkAllNotificationsAsReadCommand command, CancellationToken cancellationToken = default)
        => _service.MarkAllAsReadAsync(command.UserId, cancellationToken);
}

public sealed class UpdateNotificationEmailPreferenceHandler : IUpdateNotificationEmailPreferenceHandler
{
    private readonly INotificationService _service;
    public UpdateNotificationEmailPreferenceHandler(INotificationService service) => _service = service;
    public Task<ApiResponse<NotificationEmailPreferenceDto>> HandleAsync(UpdateNotificationEmailPreferenceCommand command, CancellationToken cancellationToken = default)
        => _service.UpdateEmailPreferenceAsync(command.UserId, command.IsEmailEnabled, command.IsTaskDeadlineReminderEmailEnabled, cancellationToken);
}